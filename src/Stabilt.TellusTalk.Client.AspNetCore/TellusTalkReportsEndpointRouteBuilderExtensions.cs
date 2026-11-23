using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client;
using Stabilt.TellusTalk.Client.AspNetCore;

#pragma warning disable IDE0130 // ASP.NET Core's endpoint mappings live here, so an app finds MapTellusTalkReports without a using.
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary><c>MapTellusTalkReports</c>, which maps the endpoint TellusTalk posts delivery reports to.</summary>
public static class TellusTalkReportsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the <c>POST</c> endpoint TellusTalk posts delivery reports to. A report without the report secret is answered
    /// <c>401</c> and a body that is not a report <c>400</c>; any other is handed to the app's
    /// <see cref="ITellusTalkReportHandler"/> and answered <c>204</c>. A handler's exception is left to the app's error
    /// handling.
    /// </summary>
    /// <param name="endpoints">The app, or a route group, to map the endpoint in.</param>
    /// <param name="pattern">
    /// The route, as the app receives it; <c>Reports:Url</c> is where TellusTalk reaches it, which a proxy may make another
    /// path.
    /// </param>
    /// <returns>
    /// The endpoint's builder, for a route group, rate limiting, or leaving it out of OpenAPI. An authorization policy put
    /// on it is not applied, since the endpoint allows anonymous requests.
    /// </returns>
    /// <exception cref="InvalidOperationException">The app's services lack <c>AddTellusTalkReports</c> or <c>AddTellusTalkClient</c>.</exception>
    public static IEndpointConventionBuilder MapTellusTalkReports(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);

        var registered = endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (!registered.IsService(typeof(TellusTalkReportsMarker)) || !registered.IsService(typeof(TellusTalkClient)))
        {
            throw new InvalidOperationException("MapTellusTalkReports needs AddTellusTalkReports and AddTellusTalkClient in the app's services.");
        }

        // The endpoint checks the report secret itself, so an app's fallback authorization policy cannot turn TellusTalk
        // away.
        return endpoints
            .MapPost(pattern, ReceiveAsync)
            .AllowAnonymous();
    }

    private static async Task ReceiveAsync(HttpContext context)
    {
        // AddTellusTalkReports, which the mapping requires, validates that Reports is set whenever the options are read.
        var reports = context.RequestServices.GetRequiredService<IOptionsMonitor<TellusTalkOptions>>().CurrentValue.Reports!;
        if (!ReportAuthorization.IsValid(context.Request.Headers.Authorization, reports))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = ReportAuthorization.Scheme;
            return;
        }

        DeliveryReport? report;
        try
        {
            report = await JsonSerializer
                .DeserializeAsync<DeliveryReport>(context.Request.Body, cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            report = null;
        }
        catch (BadHttpRequestException exception)
        {
            // The server refused the body, such as one over its size limit. Left to the app's error handling, that would
            // be logged as the app's failure and answered 500.
            context.Response.StatusCode = exception.StatusCode;
            return;
        }

        if (report is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await context.RequestServices
            .GetRequiredService<ITellusTalkReportHandler>()
            .HandleAsync(report, context.RequestAborted)
            .ConfigureAwait(false);

        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }
}

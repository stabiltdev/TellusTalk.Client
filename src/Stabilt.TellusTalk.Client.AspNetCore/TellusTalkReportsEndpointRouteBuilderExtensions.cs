using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
    /// Maps the <c>POST</c> endpoint TellusTalk posts delivery reports to at the path of <c>Reports:Url</c>, and is
    /// otherwise the overload that takes the route. The path is read when the endpoint is mapped, so a changed path needs
    /// a restart. Where the app receives on another route than TellusTalk calls, behind a proxy that rewrites paths,
    /// under <c>UsePathBase</c> or in a route group, the app gives the route to that overload.
    /// </summary>
    /// <param name="endpoints">The app to map the endpoint in.</param>
    /// <returns>
    /// The endpoint's builder, for rate limiting, or leaving it out of OpenAPI. An authorization policy put on it is not
    /// applied, since the endpoint allows anonymous requests.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The app's services lack <c>AddTellusTalkReports</c> or <c>AddTellusTalkClient</c>, <paramref name="endpoints"/> is a
    /// route group, or <c>Reports:Url</c> is not an absolute URL.
    /// </exception>
    public static IEndpointConventionBuilder MapTellusTalkReports(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        EnsureRegistered(endpoints.ServiceProvider);

        if (endpoints is RouteGroupBuilder)
        {
            throw new InvalidOperationException(
                "MapTellusTalkReports() maps the whole path of Reports:Url, which a route group's prefix would go in front of; in a route group, give it the route.");
        }

        var url = ReportUrl(endpoints.ServiceProvider);
        if (url is not { IsAbsoluteUri: true })
        {
            throw new InvalidOperationException(
                "MapTellusTalkReports() maps the path of Reports:Url, an absolute http or https URL; set it, or give the route.");
        }

        // Routing matches the request's path decoded.
        return endpoints.MapTellusTalkReports(Uri.UnescapeDataString(url.AbsolutePath));
    }

    /// <summary>
    /// Maps the <c>POST</c> endpoint TellusTalk posts delivery reports to. A report without the report secret is answered
    /// <c>401</c> and a body that is not a report <c>400</c>; any other is handed to the app's
    /// <see cref="ITellusTalkReportHandler"/> and answered <c>204</c>. A handler's exception is left to the app's error
    /// handling.
    /// </summary>
    /// <param name="endpoints">The app, or a route group, to map the endpoint in.</param>
    /// <param name="pattern">
    /// The route, as the app receives it, where it is not the path of <c>Reports:Url</c>: behind a proxy that rewrites
    /// paths, under <c>UsePathBase</c>, or in a route group.
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

        EnsureRegistered(endpoints.ServiceProvider);

        // The endpoint checks the report secret itself, so an app's fallback authorization policy cannot turn TellusTalk
        // away.
        return endpoints
            .MapPost(pattern, Receive)
            .Produces(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();
    }

    private static void EnsureRegistered(IServiceProvider services)
    {
        var registered = services.GetRequiredService<IServiceProviderIsService>();
        if (!registered.IsService(typeof(TellusTalkReportsMarker)) || !registered.IsService(typeof(TellusTalkClient)))
        {
            throw new InvalidOperationException(
                "MapTellusTalkReports needs AddTellusTalkReports and AddTellusTalkClient in the app's services.");
        }
    }

    // Without the options' validation: build-time OpenAPI generation runs the app without its secrets. The app's start
    // still validates the settings in full.
    private static Uri? ReportUrl(IServiceProvider services)
    {
        var factory = new OptionsFactory<TellusTalkOptions>(
            services.GetServices<IConfigureOptions<TellusTalkOptions>>(),
            services.GetServices<IPostConfigureOptions<TellusTalkOptions>>());

        return factory.Create(Options.DefaultName).Reports?.Url;
    }

    private static async Task<Results<
        NoContent,
        BadRequest,
        StatusCodeHttpResult,
        UnauthorizedHttpResult
    >> Receive(
        [FromServices] IOptionsMonitor<TellusTalkOptions> options,
        [FromServices] ITellusTalkReportHandler reportHandler,
        HttpContext context)
    {
        // AddTellusTalkReports, which the mapping requires, validates that Reports is set whenever the options are read.
        var reports = options.CurrentValue.Reports!;
        if (!ReportAuthorization.IsValid(context.Request.Headers.Authorization, reports))
        {
            context.Response.Headers.WWWAuthenticate = ReportAuthorization.Scheme;
            return TypedResults.Unauthorized();
        }

        DeliveryReport? report;
        try
        {
            report = await JsonSerializer.DeserializeAsync<DeliveryReport>(
                context.Request.Body,
                cancellationToken: context.RequestAborted);
        }
        catch (JsonException)
        {
            return TypedResults.BadRequest();
        }
        catch (BadHttpRequestException exception)
        {
            // The server refused the body, such as one over its size limit. Left to the app's error handling, that would
            // be logged as the app's failure and answered 500.
            return TypedResults.StatusCode(exception.StatusCode);
        }

        if (report is null)
        {
            return TypedResults.BadRequest();
        }

        await reportHandler.HandleAsync(report, context.RequestAborted);

        return TypedResults.NoContent();
    }
}

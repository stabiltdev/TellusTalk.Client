using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests.Support;

/// <summary>
/// An app on <see cref="TestServer"/> that receives reports, without the client, registered as an app registers them.
/// </summary>
internal static class ReportApp
{
    public static readonly IReadOnlyDictionary<string, string?> Settings = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["TellusTalk:Reports:Url"] = "https://app.invalid/sms/reports",
        ["TellusTalk:Reports:Secret"] = "s3cret",
        ["TellusTalk:Reports:PreviousSecrets:0"] = "0ld",
    };

    /// <summary>
    /// Starts the app with <see cref="Settings"/>; <paramref name="configure"/> may change the settings and the services,
    /// and <paramref name="map"/> replaces the mapping at the path of <c>Reports:Url</c>.
    /// </summary>
    public static async Task<WebApplication> StartAsync<THandler>(
        Action<WebApplicationBuilder>? configure = null,
        Action<WebApplication>? map = null)
        where THandler : class, ITellusTalkReportHandler
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(Settings);
        builder.Services.AddTellusTalkReports<THandler>(builder.Configuration);
        builder.Services.AddSingleton<ReceivedReports>();
        configure?.Invoke(builder);

        map ??= web => web.MapTellusTalkReports();

        var app = builder.Build();
        try
        {
            map(app);
            await app.StartAsync(TestContext.Current.CancellationToken);

            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public static async Task<HttpResponseMessage> PostAsync(
        WebApplication app,
        string body,
        string? authorization = "Report s3cret",
        string path = "/sms/reports")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        using var client = app.GetTestClient();

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

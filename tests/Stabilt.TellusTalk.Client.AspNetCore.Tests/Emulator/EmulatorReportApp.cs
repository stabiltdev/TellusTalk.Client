using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stabilt.TellusTalk.Client.AspNetCore.Tests.Support;
using Stabilt.TellusTalk.Emulator.Client;
using Stabilt.TellusTalk.Emulator.Testcontainers;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests.Emulator;

/// <summary>
/// An app on Kestrel that takes reports with <see cref="RecordingReportHandler"/>, and TellusTalk Emulator in a container
/// that reaches it through Testcontainers' <c>host.testcontainers.internal</c>.
/// </summary>
public sealed class EmulatorReportApp : IAsyncLifetime
{
    private WebApplication? _app;
    private TellusTalkEmulatorContainer? _container;

    public TellusTalkClient Client => _app!.Services.GetRequiredService<TellusTalkClient>();

    /// <summary>The emulator's read API.</summary>
    public TellusTalkEmulatorClient Api { get; private set; } = null!;

    /// <summary>The emulator's address, for the requests its read API's client has no method for.</summary>
    public HttpClient EmulatorHttp { get; private set; } = null!;

    internal ReceivedReports Received => _app!.Services.GetRequiredService<ReceivedReports>();

    public async ValueTask InitializeAsync()
    {
        // The app checks its settings when it starts, before its port is known, so it starts with a stand-in report URL.
        // The endpoint is mapped at the stand-in's path, which the URL set once the port is known keeps.
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TellusTalk:UserName"] = "tester",
            ["TellusTalk:Password"] = "secret",
            ["TellusTalk:Reports:Url"] = "http://host.testcontainers.internal/sms/reports",
            ["TellusTalk:Reports:Secret"] = "s3cret",
        });
        builder.Services.AddTellusTalkClient(builder.Configuration);
        builder.Services.AddTellusTalkReports<RecordingReportHandler>(builder.Configuration);
        builder.Services.AddSingleton<ReceivedReports>();

        _app = builder.Build();
        _app.MapTellusTalkReports();
        await _app.StartAsync();

        // A container reaches a host port only once it is exposed, and only if the container starts after that.
        var port = (ushort)new Uri(_app.Urls.Single()).Port;
        await TestcontainersSettings.ExposeHostPortsAsync(port);

        _container = new TellusTalkEmulatorBuilder()
            .WithReportDelay(TimeSpan.FromHours(1))
            .Build();

        await _container.StartAsync();

        var baseAddress = _container.GetBaseAddress();

        _app.Configuration["TellusTalk:BaseUrl"] = baseAddress;
        _app.Configuration["TellusTalk:Reports:Url"] = $"http://host.testcontainers.internal:{port}/sms/reports";
        ((IConfigurationRoot)_app.Configuration).Reload();

        Api = new TellusTalkEmulatorClient(new Uri(baseAddress));
        EmulatorHttp = new HttpClient { BaseAddress = new Uri(baseAddress) };
    }

    public async ValueTask DisposeAsync()
    {
        EmulatorHttp?.Dispose();
        Api?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

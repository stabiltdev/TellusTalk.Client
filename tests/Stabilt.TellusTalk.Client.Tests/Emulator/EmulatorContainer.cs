using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Stabilt.TellusTalk.Client.Tests.Emulator;

/// <summary>
/// TellusTalk Emulator in a container, shared by a test class. The emulator warns about every field TellusTalk's
/// specification forbids, so a message it holds without warnings is one TellusTalk would take as meant.
/// </summary>
public sealed class EmulatorContainer : IAsyncLifetime
{
    /// <summary>A released version, raised deliberately, so a new emulator release cannot change these tests unseen.</summary>
    private const string Image = "ghcr.io/stabiltdev/tellustalk-emulator:0.1.0";

    private const ushort Port = 8080;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithPortBinding(Port, assignRandomHostPort: true)
        .WithEnvironment("Emulator__ReportDelaySeconds", "3600")
        .WithWaitStrategy(Wait
            .ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request
                .ForPort(Port)
                .ForPath("/api/v1/health")))
        .Build();

    private ServiceProvider? _services;

    public Uri BaseUrl => new UriBuilder(Uri.UriSchemeHttp, _container.Hostname, _container.GetMappedPublicPort(Port)).Uri;

    public TellusTalkClient Client => _services!.GetRequiredService<TellusTalkClient>();

    /// <summary>The emulator's read API.</summary>
    public HttpClient Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TellusTalk:BaseUrl"] = BaseUrl.ToString(),
                ["TellusTalk:UserName"] = "tester",
                ["TellusTalk:Password"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTellusTalkClient(configuration);
        _services = services.BuildServiceProvider();

        Api = new HttpClient { BaseAddress = BaseUrl };
    }

    public async ValueTask DisposeAsync()
    {
        Api.Dispose();
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

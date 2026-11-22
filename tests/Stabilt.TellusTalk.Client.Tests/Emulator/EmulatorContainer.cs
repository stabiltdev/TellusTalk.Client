using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stabilt.TellusTalk.Emulator.Client;
using Stabilt.TellusTalk.Emulator.Testcontainers;

namespace Stabilt.TellusTalk.Client.Tests.Emulator;

/// <summary>
/// TellusTalk Emulator in a container, shared by a test class. The emulator warns about every field TellusTalk's
/// specification forbids, so a message it holds without warnings is one TellusTalk would take as meant.
/// </summary>
public sealed class EmulatorContainer : IAsyncLifetime
{
    private readonly TellusTalkEmulatorContainer _container = new TellusTalkEmulatorBuilder()
        .WithReportDelay(TimeSpan.FromHours(1))
        .Build();

    private ServiceProvider? _services;

    public TellusTalkClient Client => _services!.GetRequiredService<TellusTalkClient>();

    /// <summary>The emulator's read API.</summary>
    public TellusTalkEmulatorClient Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var baseAddress = _container.GetBaseAddress();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TellusTalk:BaseUrl"] = baseAddress,
                ["TellusTalk:UserName"] = "tester",
                ["TellusTalk:Password"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTellusTalkClient(configuration);
        _services = services.BuildServiceProvider();

        Api = new TellusTalkEmulatorClient(new Uri(baseAddress));
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

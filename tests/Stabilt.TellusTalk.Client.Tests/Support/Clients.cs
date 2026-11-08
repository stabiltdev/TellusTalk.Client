using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Stabilt.TellusTalk.Client.Tests.Support;

/// <summary>Registers the client as an app does, from configuration, with <see cref="RecordingHandler"/> in place of the network.</summary>
internal static class Clients
{
    public static readonly IReadOnlyDictionary<string, string?> Credentials = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["TellusTalk:UserName"] = "tester",
        ["TellusTalk:Password"] = "secret",
    };

    public static ServiceProvider Services(
        RecordingHandler handler,
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<TellusTalkOptions>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? Credentials)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services
            .AddTellusTalkClient(configure)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    public static IReadOnlyDictionary<string, string?> With(string key, string? value) =>
        new Dictionary<string, string?>(Credentials, StringComparer.Ordinal) { [key] = value };
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Stabilt.TellusTalk.Client.Tests.Support;

/// <summary>Registers the client as an app does, with <see cref="RecordingHandler"/> in place of the network.</summary>
internal static class Clients
{
    public static readonly IReadOnlyDictionary<string, string?> Credentials = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["TellusTalk:UserName"] = "tester",
        ["TellusTalk:Password"] = "secret",
    };

    public static IConfigurationRoot Configuration(IReadOnlyDictionary<string, string?>? settings = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(settings ?? Credentials)
        .Build();

    /// <summary>The client registered from the <c>TellusTalk</c> section of <paramref name="settings"/>.</summary>
    public static ServiceProvider Services(RecordingHandler handler, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = Configuration(settings);

        return Services(handler, services => services.AddTellusTalkClient(configuration));
    }

    public static ServiceProvider Services(RecordingHandler handler, Func<IServiceCollection, IHttpClientBuilder> register)
    {
        var services = new ServiceCollection();
        register(services).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    public static IReadOnlyDictionary<string, string?> With(string key, string? value) =>
        new Dictionary<string, string?>(Credentials, StringComparer.Ordinal) { [key] = value };
}

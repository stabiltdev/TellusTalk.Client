using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client;

#pragma warning disable IDE0130 // Microsoft.Extensions' registrations live here, so an app finds AddTellusTalkClient without a using.
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class TellusTalkServiceCollectionExtensions
{
    private static readonly string Version = typeof(TellusTalkClient).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion
        .Split('+')[0];

    /// <summary>
    /// Registers <see cref="TellusTalkClient"/>, configured from the section <paramref name="sectionName"/> of
    /// <paramref name="configuration"/>. Missing credentials or a base URL that is not http(s) fail when the app starts,
    /// and a change to the section reaches the next client created.
    /// </summary>
    /// <param name="configuration">
    /// The configuration that holds the section, such as the app's <c>builder.Configuration</c>. A section passed as an
    /// <see cref="IConfiguration"/> is searched for <paramref name="sectionName"/> beneath it.
    /// </param>
    /// <returns>The client's <see cref="IHttpClientBuilder"/>, for handlers of the app's own.</returns>
    public static IHttpClientBuilder AddTellusTalkClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "TellusTalk")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        return services.AddTellusTalkClient(section);
    }

    /// <summary>
    /// Registers <see cref="TellusTalkClient"/>, configured from <paramref name="section"/> itself. Missing credentials or
    /// a base URL that is not http(s) fail when the app starts, and a change to the section reaches the next client
    /// created.
    /// </summary>
    /// <returns>The client's <see cref="IHttpClientBuilder"/>, for handlers of the app's own.</returns>
    public static IHttpClientBuilder AddTellusTalkClient(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        return Register(services
            .AddOptions<TellusTalkOptions>()
            .Bind(section));
    }

    /// <summary>
    /// Registers <see cref="TellusTalkClient"/>, configured by <paramref name="configureOptions"/> alone. Missing
    /// credentials or a base URL that is not http(s) fail when the app starts.
    /// </summary>
    /// <returns>The client's <see cref="IHttpClientBuilder"/>, for handlers of the app's own.</returns>
    public static IHttpClientBuilder AddTellusTalkClient(
        this IServiceCollection services,
        Action<TellusTalkOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return Register(services
            .AddOptions<TellusTalkOptions>()
            .Configure(configureOptions));
    }

    private static IHttpClientBuilder Register(OptionsBuilder<TellusTalkOptions> options)
    {
        options.ValidateDataAnnotations();

        // Build-time OpenAPI generation starts the app without its secrets, and an app cannot take back a start-up check
        // the package adds. Creating a client still validates the options, and that run creates none.
        if (!IsOpenApiDocumentGeneration())
        {
            options.ValidateOnStart();
        }

        return options.Services.AddHttpClient<TellusTalkClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptionsMonitor<TellusTalkOptions>>().CurrentValue;

            client.BaseAddress = WithTrailingSlash(settings.BaseUrl);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")));

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                "Stabilt.TellusTalk.Client",
                Version));
        });
    }

    private static bool IsOpenApiDocumentGeneration() => string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        "GetDocument.Insider",
        StringComparison.Ordinal);

    /// <summary>Without its trailing slash, a base URL's last segment would be replaced by <c>send/v1</c> rather than kept.</summary>
    private static Uri WithTrailingSlash(Uri url) =>
        url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
}

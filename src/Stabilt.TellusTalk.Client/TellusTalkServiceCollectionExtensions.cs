using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
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
    /// Registers <see cref="TellusTalkClient"/>, configured from the <c>TellusTalk</c> section and then by
    /// <paramref name="configure"/>. Missing credentials or a base URL that is not http(s) fail at startup.
    /// </summary>
    /// <returns>The client's <see cref="IHttpClientBuilder"/>, for handlers of the app's own.</returns>
    public static IHttpClientBuilder AddTellusTalkClient(
        this IServiceCollection services,
        Action<TellusTalkOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services
            .AddOptions<TellusTalkOptions>()
            .BindConfiguration(TellusTalkOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            options.Configure(configure);
        }

        return services.AddHttpClient<TellusTalkClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptionsMonitor<TellusTalkOptions>>().CurrentValue;

            client.BaseAddress = WithTrailingSlash(settings.BaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Stabilt.TellusTalk.Client", Version));
        });
    }

    /// <summary>Without its trailing slash, a base URL's last segment would be replaced by <c>send/v1</c> rather than kept.</summary>
    private static Uri WithTrailingSlash(Uri url) =>
        url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
}

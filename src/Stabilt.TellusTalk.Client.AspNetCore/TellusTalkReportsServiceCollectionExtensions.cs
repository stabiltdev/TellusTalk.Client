using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stabilt.TellusTalk.Client;
using Stabilt.TellusTalk.Client.AspNetCore;

#pragma warning disable IDE0130 // Microsoft.Extensions' registrations live here, so an app finds AddTellusTalkReports without a using.
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// <c>AddTellusTalkReports</c>, which registers the app's handler of TellusTalk's delivery reports and the report
/// settings.
/// </summary>
public static class TellusTalkReportsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> as the app's <see cref="ITellusTalkReportHandler"/>, scoped, for the
    /// endpoint <c>MapTellusTalkReports</c> maps, and the report settings from <c>Reports</c> in the section
    /// <paramref name="sectionName"/> of <paramref name="configuration"/>, the section <c>AddTellusTalkClient</c> reads.
    /// Only <c>Reports:Secret</c> is required; a missing secret, or a setting that breaks its rule, fails when the app
    /// starts, and a secret changed in the section reaches the next report.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="configuration">
    /// The configuration that holds the section, such as the app's <c>builder.Configuration</c>. A section passed as an
    /// <see cref="IConfiguration"/> is searched for <paramref name="sectionName"/> beneath it.
    /// </param>
    /// <param name="sectionName">The section's name, <c>TellusTalk</c> unless given.</param>
    /// <returns>The app's services.</returns>
    public static IServiceCollection AddTellusTalkReports<THandler>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "TellusTalk")
        where THandler : class, ITellusTalkReportHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        return services.AddTellusTalkReports<THandler>(section);
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as the app's <see cref="ITellusTalkReportHandler"/>, scoped, for the
    /// endpoint <c>MapTellusTalkReports</c> maps, and the report settings from <c>Reports</c> in
    /// <paramref name="section"/>, the section <c>AddTellusTalkClient</c> reads rather than its <c>Reports</c>. Only
    /// <c>Reports:Secret</c> is required; a missing secret, or a setting that breaks its rule, fails when the app starts,
    /// and a secret changed in the section reaches the next report.
    /// </summary>
    /// <returns>The app's services.</returns>
    public static IServiceCollection AddTellusTalkReports<THandler>(
        this IServiceCollection services,
        IConfigurationSection section)
        where THandler : class, ITellusTalkReportHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        return Register<THandler>(services
            .AddOptions<TellusTalkReportOptions>()
            .Bind(section.GetSection("Reports")));
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as the app's <see cref="ITellusTalkReportHandler"/>, scoped, for the
    /// endpoint <c>MapTellusTalkReports</c> maps, and the report settings as <paramref name="configureOptions"/> sets
    /// them. Only <see cref="TellusTalkReportOptions.Secret"/> is required; a missing secret, or a setting that breaks its
    /// rule, fails when the app starts.
    /// </summary>
    /// <returns>The app's services.</returns>
    public static IServiceCollection AddTellusTalkReports<THandler>(
        this IServiceCollection services,
        Action<TellusTalkReportOptions> configureOptions)
        where THandler : class, ITellusTalkReportHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return Register<THandler>(services
            .AddOptions<TellusTalkReportOptions>()
            .Configure(configureOptions));
    }

    private static IServiceCollection Register<THandler>(OptionsBuilder<TellusTalkReportOptions> options)
        where THandler : class, ITellusTalkReportHandler
    {
        options.ValidateDataAnnotations();

        // Build-time OpenAPI generation starts the app without its secrets, and an app cannot take back a start-up check
        // the package adds. Taking a report still validates the settings, and that run takes none.
        if (!IsOpenApiDocumentGeneration())
        {
            options.ValidateOnStart();
        }

        options.Services.AddScoped<ITellusTalkReportHandler, THandler>();
        options.Services.TryAddSingleton(new TellusTalkReportsMarker());

        return options.Services;
    }

    private static bool IsOpenApiDocumentGeneration() => string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        "GetDocument.Insider",
        StringComparison.Ordinal);
}

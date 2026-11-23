using Microsoft.Extensions.DependencyInjection.Extensions;
using Stabilt.TellusTalk.Client;
using Stabilt.TellusTalk.Client.AspNetCore;

#pragma warning disable IDE0130 // Microsoft.Extensions' registrations live here, so an app finds AddTellusTalkReports without a using.
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class TellusTalkReportsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> as the app's <see cref="ITellusTalkReportHandler"/>, scoped, for the
    /// endpoint <c>MapTellusTalkReports</c> maps. It needs <c>AddTellusTalkClient</c> and its <c>Reports</c> settings; an
    /// app without them fails when it starts.
    /// </summary>
    public static IServiceCollection AddTellusTalkReports<THandler>(this IServiceCollection services)
        where THandler : class, ITellusTalkReportHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITellusTalkReportHandler, THandler>();
        services.TryAddSingleton(new TellusTalkReportsMarker());

        services
            .AddOptions<TellusTalkOptions>()
            .Validate(options => options.Reports is not null, "TellusTalk's reports need the Reports settings, Reports:Url and Reports:Secret.");

        return services;
    }
}

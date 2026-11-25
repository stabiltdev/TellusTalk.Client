namespace Stabilt.TellusTalk.Client.AspNetCore;

/// <summary>
/// The app's handling of a delivery report that came with the report secret. Registered with
/// <c>AddTellusTalkReports</c>, scoped, so it takes the app's services through its constructor.
/// </summary>
public interface ITellusTalkReportHandler
{
    /// <summary>
    /// Handles <paramref name="report"/>; the endpoint answers <c>200</c> when it returns. TellusTalk posts a report once,
    /// so a report is lost when this throws: it should hand the report on quickly, such as to a queue, before doing
    /// anything that can fail.
    /// </summary>
    Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken);
}

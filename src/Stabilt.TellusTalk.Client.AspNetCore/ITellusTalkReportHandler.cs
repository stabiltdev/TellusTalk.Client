namespace Stabilt.TellusTalk.Client.AspNetCore;

/// <summary>
/// The app's handling of a delivery report that came with the report secret. Registered with
/// <c>AddTellusTalkReports</c>, scoped, so it takes the app's services through its constructor.
/// </summary>
public interface ITellusTalkReportHandler
{
    /// <summary>
    /// Handles <paramref name="report"/>; the endpoint answers <c>204</c> when it returns. It should hand the report on
    /// quickly, such as to a queue, and take the same report twice without harm: whether TellusTalk sends a report again
    /// after a failed answer is not known.
    /// </summary>
    Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken);
}

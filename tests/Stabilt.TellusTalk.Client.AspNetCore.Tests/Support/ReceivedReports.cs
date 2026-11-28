using System.Collections.Concurrent;
using System.Diagnostics;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests.Support;

/// <summary>The reports the app's handler was given, kept for the test to read.</summary>
internal sealed class ReceivedReports
{
    public ConcurrentQueue<DeliveryReport> Reports { get; } = new();

    /// <summary>Each report with the activity of the request its handler ran in, if the request had one.</summary>
    public ConcurrentQueue<(DeliveryReport Report, Activity? Request)> Requests { get; } = new();
}

internal sealed class RecordingReportHandler(ReceivedReports received) : ITellusTalkReportHandler
{
    public Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken)
    {
        // Before Reports, which the tests wait on.
        received.Requests.Enqueue((report, Activity.Current));
        received.Reports.Enqueue(report);

        return Task.CompletedTask;
    }
}

internal sealed class FailingReportHandler : ITellusTalkReportHandler
{
    public Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException("The app's handler failed."));
}

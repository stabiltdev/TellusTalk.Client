using System.Collections.Concurrent;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests.Support;

/// <summary>The reports the app's handler was given, kept for the test to read.</summary>
internal sealed class ReceivedReports
{
    public ConcurrentQueue<DeliveryReport> Reports { get; } = new();
}

internal sealed class RecordingReportHandler(ReceivedReports received) : ITellusTalkReportHandler
{
    public Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken)
    {
        received.Reports.Enqueue(report);

        return Task.CompletedTask;
    }
}

internal sealed class FailingReportHandler : ITellusTalkReportHandler
{
    public Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException("The app's handler failed."));
}

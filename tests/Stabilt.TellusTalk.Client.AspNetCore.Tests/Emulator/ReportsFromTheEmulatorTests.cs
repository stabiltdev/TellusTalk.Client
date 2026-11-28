using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Stabilt.TellusTalk.Emulator.Client;

namespace Stabilt.TellusTalk.Client.AspNetCore.Tests.Emulator;

/// <summary>
/// A message sent by a client whose report settings give it the report address, the emulator's report of it, and the
/// app's handler given that report. Each test sends to a number of its own, and the emulator reports at once on request.
/// </summary>
public sealed class ReportsFromTheEmulatorTests(EmulatorReportApp app) : IClassFixture<EmulatorReportApp>
{
    [Fact]
    public async Task A_delivered_report_from_the_emulator_reaches_the_handler()
    {
        var result = await app.Client.SendAsync(
            new SmsMessage
            {
                To = ["+46701740620"],
                Text = "Din tid är bekräftad.",
                Echo = JsonSerializer.SerializeToElement(new { appointment = 4711 }),
            },
            TestContext.Current.CancellationToken);

        var report = await ReportAsync("+46701740620", "delivered");

        report.Status.Should().Be(DeliveryStatus.Delivered);
        report.LogUuid.Should().Be(result.LogUuid);
        report.Echo!.Value.GetProperty("appointment").GetInt32().Should().Be(4711);
    }

    [Fact]
    public async Task A_failed_report_from_the_emulator_reaches_the_handler()
    {
        await app.Client.SendAsync(
            new SmsMessage { To = ["+46701740621"], Text = "Din tid är bekräftad." },
            TestContext.Current.CancellationToken);

        var report = await ReportAsync("+46701740621", "failed");

        report.Status.Should().Be(DeliveryStatus.Failed);
        report.FailedReason.Should().Be("txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None");
    }

    [Fact]
    public async Task A_report_from_the_emulator_runs_in_the_trace_of_its_send()
    {
        using var send = new Activity("send");
        send.Start();
        await app.Client.SendAsync(
            new SmsMessage { To = ["+46701740622"], Text = "Din tid är bekräftad." },
            TestContext.Current.CancellationToken);
        send.Stop();

        // The emulator passes on the trace of the call that has it post the report; that call runs in a trace of its
        // own, so the send's trace reaches the handler only through the report address.
        using (var report = new Activity("report"))
        {
            report.Start();
            await ReportAsync("+46701740622", "delivered");
        }

        var request = app.Received.Requests
            .Single(received => string.Equals(received.Report.To, "+46701740622", StringComparison.Ordinal))
            .Request;
        request.Should().NotBeNull();
        request!.TraceId.Should().Be(send.TraceId);
        request.ParentSpanId.Should().Be(send.SpanId);
    }

    /// <summary>
    /// Has the emulator report the message to <paramref name="to"/> with <paramref name="status"/>, and waits for the
    /// handler to be given it; a report that never comes fails with the emulator's attempts, which say whether it reached
    /// the app.
    /// </summary>
    private async Task<DeliveryReport> ReportAsync(string to, string status)
    {
        var messages = await app.Api.GetMessagesAsync(new MessageQuery { To = to }, TestContext.Current.CancellationToken);
        var message = messages.Should().ContainSingle().Subject;
        message.Warnings.Should().BeEmpty();

        using var response = await app.EmulatorHttp.PostAsJsonAsync(
            new Uri($"api/messages/{message.Id}/reports", UriKind.Relative),
            new { status },
            TestContext.Current.CancellationToken);

        var attempts = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue("the emulator answered {0}", attempts);

        var waited = Stopwatch.StartNew();
        while (Received(to) is null && waited.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        var report = Received(to);
        report.Should().NotBeNull("the emulator's attempts were {0}", attempts);

        return report!;
    }

    private DeliveryReport? Received(string to) => app.Received.Reports.FirstOrDefault(report => string.Equals(report.To, to, StringComparison.Ordinal));
}

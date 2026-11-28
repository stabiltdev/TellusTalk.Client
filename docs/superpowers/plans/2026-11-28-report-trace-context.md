# A Send's Trace Carried to Its Delivery Reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A report's request belongs to the trace of the send it reports on, so the handler's logs carry the send's
`TraceId`.

**Architecture:** `TellusTalkClient.SendAsync` adds `traceparent`, the current activity's W3C id, to the headers of the
report address it builds from `Reports`. K8 puts those headers on the report's request, and the receiver's own hosting
reads `traceparent` as the request's parent; no code changes on the receiving side.

**Tech Stack:** .NET 10, `System.Diagnostics.Activity`, ASP.NET Core minimal endpoints, xUnit v3 on Microsoft Testing
Platform, FluentAssertions 8, Testcontainers with TellusTalk Emulator.

**Spec:** `docs/superpowers/specs/2026-11-28-report-trace-context-design.md`

## Global Constraints

- Work in `E:\.worktrees\TellusTalk.Client\report-trace-context` on `feat/report-trace-context`, and run `dotnet` from
  there: `global.json` sets the test runner.
- `net10.0`; no package is added or raised. `Activity` is in the shared framework.
- Only `traceparent` is sent: never `tracestate` or `baggage`. Not through `DistributedContextPropagator`, which writes
  `baggage` too.
- No setting, no public member added or changed; the addresses of `SmsMessage.ReportTo` are sent as they are.
- The repository's conventions hold (`docs/conventions.md`): one call per line in a chain, LINQ as a method chain,
  comments only for what the code cannot say, test names that state the rule, every public member with an XML summary,
  numbers from `+46701740605` to `+46701740699`.
- Everything written is English. No customer of TellusTalk is named anywhere.
- Each task ends green on `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`,
  `dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes` and
  `dotnet test --solution Stabilt.TellusTalk.Client.slnx` (Docker running; start Docker Desktop if `docker ps` cannot
  reach it), and is committed with a Conventional Commits message, lowercase after the colon, ending in
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` and no session link.

## Review Focus

- A send within an activity from an app without `Reports` adds no address at all: the message's own `ReportTo` alone
  — Task 1, `Without_report_settings_a_message_sent_within_an_activity_carries_only_its_own_report_addresses`.
- The tests that pin `report_to` exactly today stay green unchanged, which shows the runner sets no ambient activity. If
  one fails because an activity is current, stop and report it rather than loosen the test — Task 1, Step 5.
- The listener the emulator's app adds makes ASP.NET Core create activities for every app in the test process, the
  `TestServer` apps included: it listens to `Microsoft.AspNetCore` alone, is disposed with the fixture, and the whole
  suite stays green — Task 1, Step 5.

## Files

| Path | Change |
|---|---|
| `src/Stabilt.TellusTalk.Client/TellusTalkClient.cs` | `traceparent` in the report address; `SendAsync`'s summary |
| `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs` | The address with and without an activity |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReceivedReports.cs` | Each report with the activity its handler ran in |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs` | Listens to ASP.NET Core's activities |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/ReportsFromTheEmulatorTests.cs` | The report in the send's trace |
| `README.md` | The paragraph on the trace the report address carries |
| `docs/decisions/20261128-carry-a-sends-trace-to-its-delivery-reports-in-traceparent.md`, `docs/decisions/README.md` | New record and its row |

---

### Task 1: The report address carries the send's `traceparent`

**Files:**
- Modify: `src/Stabilt.TellusTalk.Client/TellusTalkClient.cs:16-20,50-54`
- Modify: `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs`
- Modify: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReceivedReports.cs`,
  `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs`,
  `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/ReportsFromTheEmulatorTests.cs`

**Interfaces:**
- Produces: no public surface. In the test project, `ReceivedReports.Requests`, a
  `ConcurrentQueue<(DeliveryReport Report, Activity? Request)>`.

- [ ] **Step 1: Write the client's tests in `ReportSettingsTests`**

Each registers the client with `Clients.WithReports()` unless it says otherwise, sends `Message`, and compares
`Field(handler, "report_to")`. `Authorization` alone is
`["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports"]`.

```csharp
[Theory]
[InlineData(ActivityTraceFlags.Recorded, "01")]
[InlineData(ActivityTraceFlags.None, "00")]
public async Task A_message_sent_within_an_activity_carries_its_trace_parent_to_the_report(ActivityTraceFlags flags, string sampled)
{
    // … client with reports
    using var activity = new Activity("send");
    activity.ActivityTraceFlags = flags;
    activity.Start();

    await client.SendAsync(Message, TestContext.Current.CancellationToken);

    Field(handler, "report_to").Should().Be(
        $$$"""["https:{\"headers\":{\"Authorization\":\"Report s3cret\",\"traceparent\":\"00-{{{activity.TraceId}}}-{{{activity.SpanId}}}-{{{sampled}}}\"}}://app.invalid/sms/reports"]""");
}
```

| Test | Within | Expected `report_to` |
|---|---|---|
| `A_message_sent_outside_any_activity_carries_the_secret_alone` | `Activity.Current = null;` first | `Authorization` alone |
| `A_message_sent_within_a_hierarchical_activity_carries_the_secret_alone` | `new Activity("send")`, `SetIdFormat(ActivityIdFormat.Hierarchical)`, started | `Authorization` alone |
| `The_messages_own_report_addresses_carry_no_trace_parent` | a started `Activity`; `Message with { ReportTo = [ReportAddress.Url(new Uri("https://other.invalid/reports"))] }` | its second element is `"https://other.invalid/reports"` |
| `Without_report_settings_a_message_sent_within_an_activity_carries_only_its_own_report_addresses` | `Clients.Services(handler)`, no reports; a started `Activity`; `ReportTo = [ReportAddress.Email("reports@app.invalid")]` | `["email:reports@app.invalid"]` |

- [ ] **Step 2: Write the emulator's test of the handler's trace**

`ReceivedReports` gains `Requests`, and `RecordingReportHandler` enqueues `(report, Activity.Current)` there before it
enqueues the report in `Reports`, since the tests wait on `Reports`. `EmulatorReportApp` adds an `ActivityListener` for
the source `Microsoft.AspNetCore` alone, sampling `AllData`, before the app starts, and disposes it in `DisposeAsync`:
ASP.NET Core gives a request an activity only when something listens or logs.

In `ReportsFromTheEmulatorTests`:

```csharp
[Fact]
public async Task A_report_from_the_emulator_runs_in_the_trace_of_its_send()
{
    using var send = new Activity("send").Start();
    await app.Client.SendAsync(
        new SmsMessage { To = ["+46701740622"], Text = "Din tid är bekräftad." },
        TestContext.Current.CancellationToken);
    send.Stop();

    // The emulator passes on the trace of the call that has it post the report; that call runs in a trace of its own,
    // so the send's trace reaches the handler only through the report address.
    using (new Activity("report").Start())
    {
        await ReportAsync("+46701740622", "delivered");
    }

    var request = app.Received.Requests
        .Single(received => string.Equals(received.Report.To, "+46701740622", StringComparison.Ordinal))
        .Request;
    request.Should().NotBeNull();
    request!.TraceId.Should().Be(send.TraceId);
    request.ParentSpanId.Should().Be(send.SpanId);
}
```

- [ ] **Step 3: Run the new tests to see them fail**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class Stabilt.TellusTalk.Client.Tests.ReportSettingsTests`
Expected: both cases of `A_message_sent_within_an_activity_carries_its_trace_parent_to_the_report` FAIL, their
`report_to` without `traceparent`; every other test PASSES.

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class Stabilt.TellusTalk.Client.AspNetCore.Tests.Emulator.ReportsFromTheEmulatorTests`
Expected: `A_report_from_the_emulator_runs_in_the_trace_of_its_send` FAILS on `TraceId`, the request being in the
`report` activity's trace; the two other tests PASS. A failure on `NotBeNull` instead means the listener does not
reach the app's requests: fix that before going on.

- [ ] **Step 4: Add `traceparent` in `TellusTalkClient.ReportAddressOf`**

`ReportAddressOf(TellusTalkReportOptions? reports)` keeps its signature and reads `Activity.Current`. When it is
`{ IdFormat: ActivityIdFormat.W3C }`, the headers hold `["traceparent"]` = `00-<trace-id>-<span-id>-<flags>` after
`Authorization`; `Activity.Id` or the activity's context, as the builder prefers. A comment says why
`DistributedContextPropagator` is not used: it writes `baggage` too, and K8 stores the address with the message.

`SendAsync`'s summary gains: sent within an activity in W3C format, the report address also carries the activity's id
as `traceparent`, so the report's request joins the send's trace.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: PASS, every test; the tests that pinned `report_to` before this task unchanged and green.

- [ ] **Step 6: Build like CI and check the format**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release` and
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`
Expected: no warnings, no errors, no format changes.

- [ ] **Step 7: Commit**

```bash
git add src/Stabilt.TellusTalk.Client/TellusTalkClient.cs tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReceivedReports.cs tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/ReportsFromTheEmulatorTests.cs
git commit -m "feat: carry a send's trace to its delivery reports" -m "The report address carries traceparent from the activity the send runs in, so the receiver's hosting makes the report's request a child of that span and the handler's logs carry the send's trace id. Only traceparent goes: K8 stores the address with the message, which keeps baggage out, and tracestate may hold spaces." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: The README and the decision record

**Files:**
- Modify: `README.md:139-148`
- Create: `docs/decisions/20261128-carry-a-sends-trace-to-its-delivery-reports-in-traceparent.md`
- Modify: `docs/decisions/README.md`

**Interfaces:**
- Consumes: the behaviour of Task 1.

- [ ] **Step 1: Add the README's paragraph**

After the paragraph that opens "When `Reports` is set, the client opens every message's `report_to`", a paragraph of
its own:

> When the send runs within an `Activity`, as a request or a traced job does, the report address also carries the
> activity's id as `traceparent`, which TellusTalk sends back with the report. ASP.NET Core, or any receiver that reads
> W3C trace context, makes the report's request a child of the span the send ran in, so the handler's logs carry the
> send's `TraceId`, though the report may come days later. Only `traceparent` goes, not `tracestate` or `baggage`,
> since TellusTalk stores the address with the message. The addresses a message's `ReportTo` adds are sent as they are.

- [ ] **Step 2: Commit the README**

```bash
git add README.md
git commit -m "docs: describe the trace a delivery report carries back" -m "An app that upgrades sees its report requests join the trace of their send; the README says so where it describes the report address." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: Write the decision record from `docs/decisions/template.md`**

Status `accepted`, date `2026-11-28`, deciders `[Peter Hedberg]`, no `supersedes`. Title: "Carry a send's trace to its
delivery reports in traceparent".

- **Context:** a report reaches the app as a request of its own, possibly days after the send, in a trace of its own;
  joining the two takes the app logging `JobUuid` on both sides. K8 puts every header of a URL address's settings on
  the report's request, as written.
- **Considered options:** `traceparent` in the report address, as the parent; a header of the client's own and a span
  link added by the endpoint; the trace context in `Echo`; the context written through `DistributedContextPropagator`;
  `tracestate` and `baggage` carried too; a setting to turn it off.
- **Decision, in bold first:** when `Reports` is set and the send runs within an activity in W3C format, the report
  address carries the activity's id as `traceparent`, and nothing else of the trace. Then why: the standard header is
  read by every receiver's hosting, so the receiving side needs no code; a link keeps the report in a trace of its own,
  joined only in a backend that shows links; `Echo` is the app's; `baggage` is the app's data and would be stored at
  TellusTalk, and `tracestate` may hold spaces; a way to turn it off can be added later without breaking an app.
- **Consequences:** Good: the handler's logs carry the send's `TraceId` with no code, in any receiver. Bad: a trace
  spans days, and a collector's tail sampling has decided before the report comes; the parent is the app's span, not
  the HTTP call to K8; an app cannot turn it off. Follow-up: a send to K8 confirms that `traceparent` arrives as
  written, since K8's sender would set its own if it had tracing instrumentation.

Add its row to the table in `docs/decisions/README.md`, status `accepted`.

- [ ] **Step 4: Check the record's links**

Run: `grep -n "20261128-carry-a-sends-trace-to-its-delivery-reports-in-traceparent" docs/decisions/README.md` and
`ls docs/decisions/20261128-carry-a-sends-trace-to-its-delivery-reports-in-traceparent.md`
Expected: the row's link and label both name the file, and the file exists.

- [ ] **Step 5: Commit the record**

```bash
git add docs/decisions/20261128-carry-a-sends-trace-to-its-delivery-reports-in-traceparent.md docs/decisions/README.md
git commit -m "docs: record carrying a send's trace to its reports in traceparent" -m "The record holds why the report joins the send's trace as a child rather than through a link, and why nothing but traceparent goes into an address K8 stores." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

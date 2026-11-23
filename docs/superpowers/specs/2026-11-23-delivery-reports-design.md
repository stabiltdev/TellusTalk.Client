# Delivery reports

## Intent

An app that sends SMS through the client wants to know what became of each one. TellusTalk's K8 tells it by posting a
delivery report to each URL in the message's `report_to`. Today the client can put such a URL in a message
(`ReportAddress.Url`, with headers), but the app has to write the endpoint, check that a report is genuine, and read
K8's JSON itself.

The first app to use it answers each request to send an SMS with sent, delivered or failed: sent comes from the send's
answer, and delivered and failed need the reports. The package is public, so any ASP.NET Core app should get a safe
report endpoint in a few lines.

## What K8 was seen to do

From TellusTalk Emulator's `docs/k8-behaviour.md` and README (`E:\Stabilt\TellusTalk.Emulator`):

- A report is a `POST` of JSON to each http(s) URL in `report_to`, one per recipient, with
  `Content-Type: application/json`, `Accept: */*`, `webpages-request-id` and `x-webpages-request-id`, and every header
  the address's settings name. A URL with headers is written `https:{"headers":{"Name":"value"}}://host/path`, which
  `ReportAddress.Url(url, headers)` already writes.
- The body: `log_status`, `log_status_code`, `failed_reason`, `article_id`, `to_protocol`, `to_address`
  (`sms:+46…`), `to_name`, `from_address`, `from_name`, `subject`, `text`, `html`, `echo` (as the request sent it),
  `quantity` (the number of parts), `log_uuid`, `conversation_uuid`, `job_uuid`, `report_time` (ISO 8601 in UTC, or
  `null`), `failed_details`, `text_linebreaksbr`, `template_optional_line`. The README's example shows them all.
- Three statuses were seen: `txtDELIVERED` with code `22`; `txtDELIVERY_FAILED` with `21` and a `failed_reason` such as
  `txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None`; and `txtBLOCKED` with `1` for a send held back as a duplicate,
  reported at once with `report_time` `null`.
- K8 signs nothing. The only sign that a report comes from TellusTalk is a header the app itself put in the report
  address.
- Not seen: reports before the final one, other statuses, whether K8 sends a report again when the receiver fails,
  reports for a number K8 blocks (`invalid_recipients`), and reports on other channels.
- `log_uuid` is in the send's answer only for a send to one recipient; with several, each report carries a `log_uuid`
  the app has not seen. `echo` comes back unchanged in every case.

## Goals

- An app registers a handler class and maps an endpoint; the package reads each report, checks that it is genuine,
  and hands it to the handler.
- The secret that makes a report genuine is configured once, beside the client's other settings, and the app gets the
  report address carrying it ready-made, so the two ends cannot disagree.
- A report whose status the package does not know still reaches the handler.
- An app that does not run ASP.NET Core can still read a report with the main package alone.
- The emulator's tests show the whole path: a message sent with the report address, the emulator's report, the
  handler called with it.

## Non-goals

- Reports by e-mail or SMS, reports in other `content_type`s, and reports for other channels; none has been seen.
- Replies to a `SmsSender.ReplyNumber`; what a reply holds has not been seen.
- Storing reports, retrying them, or mapping them to an app's own outcomes.
- Adding the report address to messages on the app's behalf; the app puts it in `ReportTo` itself.

## Anti-goals

- An endpoint anyone can post to and mark an SMS delivered.
- The package logging a report's number, text or secret; a report carries personal data.
- A report the package cannot place being dropped without the app knowing.

## Approaches

**How much the package takes on.**

- A. Reading only: a `DeliveryReport` type in the main package; each app writes its endpoint and its check of the
  secret. Small, and fits any framework, but every app writes the check that matters most, and a mistake opens the
  endpoint to anyone.
- B. Reading and receiving: the reading stays in the main package, and a package of its own,
  `Stabilt.TellusTalk.Client.AspNetCore`, maps the endpoint, checks the secret and hands the report to the app. One more
  package to keep, and the only one that depends on ASP.NET Core.

**Chosen: B**, so the check is written once and the main package stays free of ASP.NET Core.

**How the app receives a report.** A delegate given to the mapping, or a handler class the package resolves from DI.
**Chosen: a handler class**, `ITellusTalkReportHandler`, proposed by the user: it takes the app's services through its
constructor, such as a `DbContext` or Wolverine's `IMessageBus`, and is tested on its own. The interface has a purpose
here: the package calls an implementation the app writes.

**Where the secret lives.**

1. The client's settings hold the report URL and the secret, and the app gets a `ReportAddress` with the secret's header
   to put in `ReportTo`.
2. The settings hold the secret for the check only, and the app builds the report address itself with the same secret.

**Chosen: 1**, so the secret, and the header's name, are in one place.

## The design

### Shape for an app

```json
{
  "TellusTalk": {
    "UserName": "…",
    "Password": "…",
    "Reports": {
      "Url": "https://app.example/sms/reports",
      "Secret": "…"
    }
  }
}
```

```csharp
builder.Services.AddTellusTalkClient(builder.Configuration);
builder.Services.AddTellusTalkReports<SmsReportHandler>();

app.MapTellusTalkReports("/sms/reports");

public sealed class SmsReportHandler(IMessageBus bus) : ITellusTalkReportHandler
{
    public Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken) => …;
}

// Sending:
new SmsMessage { To = […], Text = …, ReportTo = [tellusTalk.ReportAddress], Echo = … };
```

### Settings (main package)

- `TellusTalkOptions.Reports` is optional, with `Url` (the absolute http(s) URL TellusTalk posts to, as the world
  reaches the app), `Secret`, and `PreviousSecrets`. It is bound by `AddTellusTalkClient` with the rest of the section,
  in each of its forms, so a section of the app's own name holds `Reports` too.
- When `Reports` is present, `Url` and `Secret` are required and `Url` is absolute http(s); a report setting that is
  wrong fails when the app starts, like the client's other settings.
- `PreviousSecrets` exists because a report carries the secret its message was sent with, and a message's report can
  come days after it was sent (`sms_validity_time`): the endpoint takes the current secret and the previous ones, so a
  rotation does not turn away the reports of messages already sent. The settings are read through
  `IOptionsMonitor`, as the client's are, so a rotation needs no restart.
- `TellusTalkClient.ReportAddress` is `ReportAddress.Url(Reports.Url, { ["Authorization"] = "Report " + Secret })`,
  read from the current settings each time; without `Reports` it throws `InvalidOperationException` saying the setting
  is missing. The client takes `IOptionsMonitor<TellusTalkOptions>` beside its `HttpClient`.
- The secret goes in `Authorization` (decided with the user): it is what the header is for, and logging tools, ASP.NET
  Core's HTTP logging among them, treat it as a credential and leave its value out, as they would not a header of the
  package's own.
- The scheme is `Report` (my call), not `Bearer`: JWT bearer authentication in the app reads every `Bearer` value as a
  token, fails on the secret and logs it for each report, while it leaves another scheme alone.
- K8 was seen sending every header an address's settings name, and every example in both repositories uses
  `Authorization`, though the names tried were not recorded; the receiver's log kept from the report probes of
  2026-11-14 holds no such header, so the observation rests on 2026-11-07 alone. A send to K8 whose report address carries
  `Authorization: Report <secret>` confirms that the value arrives unchanged before the first release; the probe is with
  the emulator's maintainers.

### `DeliveryReport` (main package)

- A record that `System.Text.Json` reads from K8's body, so an app outside ASP.NET Core can read one with
  `JsonSerializer.Deserialize<DeliveryReport>`.
- It holds what an app needs to place a report and act on it (my call on the set):
  - `Status`: `Delivered`, `Failed`, `Blocked` or `Unknown`, from `log_status`; `StatusText` and `StatusCode` keep
    K8's `log_status` and `log_status_code` as they came, so an unknown status can still be read.
  - `FailedReason` and `FailedDetails`: K8's text, or `null` when empty.
  - `To`: the recipient's number as the app gave it, without K8's `sms:`; the client adds that prefix, so the report
    gives back what the message held. An address that is not `sms:` is kept whole.
  - `JobUuid`, `LogUuid`, `Echo` (`JsonElement?`), `Parts` (from `quantity`), `ReportTime` (`DateTimeOffset?`).
- It leaves out the message's text, subject and html: the app has them already, and a property would invite their use
  and their logging. It leaves out the fields that say nothing for SMS, such as `article_id` and
  `template_optional_line`.
- `log_status` is the one field a report cannot do without; a body without it is not a report.

### `Stabilt.TellusTalk.Client.AspNetCore`

- `AddTellusTalkReports<THandler>()` registers `THandler` as `ITellusTalkReportHandler`, scoped, so it gets a scope per
  report. It requires `AddTellusTalkClient` (it reads `TellusTalkOptions`), and adds a start-up check that `Reports` is
  set, so an app that maps the endpoint without the setting fails at start rather than at its first report.
- The secret cannot be left out, and nothing turns the check off (decided with the user). The endpoint has to be
  reachable from the internet for TellusTalk to post to it, so without the check anyone who finds it can mark an SMS
  delivered or failed. A way to turn the check off, for an app that guards the endpoint otherwise, can be added later
  without breaking any app; making the secret required after it had been optional would break every app without one.
- `MapTellusTalkReports(pattern)` maps a `POST` endpoint and returns its `IEndpointConventionBuilder`, so the app can
  put it in a route group, keep it out of OpenAPI or add rate limiting. Without `AddTellusTalkReports` it throws when it
  is mapped, as `MapHealthChecks` does without `AddHealthChecks`. The endpoint is `AllowAnonymous`, so an app's
  fallback authorization policy does not turn TellusTalk away; it does its own check instead.
- The endpoint, for each request:
  - an `Authorization` that is missing, of another scheme than `Report`, or whose value matches neither the secret nor
    a previous one → `401`, and the handler is not called; the comparison takes the same time whatever the value.
  - a body that is not JSON, or has no `log_status` → `400`.
  - otherwise the handler is called with the report and the request's `CancellationToken`; when it returns → `204`,
    since the answer carries nothing. An exception is left to the app's own error handling, which answers `500`.
- Neither TellusTalk's documentation nor the probes say what K8 does with an answer other than `200`. The codes follow
  how webhooks are commonly answered, so they hold either way: `204` takes the report, `400` and `401` say that sending
  it again changes nothing, and `500` asks for it again. If K8 never sends again, the `400`, `401` and `500` still show
  a lost report in the app's telemetry, such as a secret changed in one place and not the other, where answering `204`
  regardless would hide it. A probe of K8 against a receiver that answers `500`, `401` or nothing is with the
  emulator's maintainers; the codes stand until it shows whether K8 sends a report again (decided with the user), and
  are reconsidered if K8 does something harmful with them, such as turning the address off.
- `Reports.Url` and the mapped pattern are both stated by the app: behind a proxy that rewrites paths they differ, so
  neither is derived from the other.
- The package never logs a report's body or the secret.
- The README says the handler should hand the report on quickly, for example to a queue, and be idempotent: whether K8
  sends a report again when the endpoint fails is not known, and the emulator can send one any number of times.

### Correlation

The README shows two ways to tie a report to its message: `Echo` with the app's own id, which works for any number of
recipients, or `LogUuid` against the send's `SendResult.LogUuid`, for a send to one recipient.

## Constraints

- The repository's rules hold: refuse what TellusTalk's specification forbids where a value is set, never retry a
  send, never borrow models from the emulator, no personal data or customer names in code, tests or documents, test
  numbers from `+46701740605` to `+46701740699`.
- `net10.0`, as the main package. The new package references the main one and the ASP.NET Core shared framework.
- Release builds treat warnings as errors, and `dotnet format --verify-no-changes` must pass.
- The two packages are released together, at one version; CI packs both.

## Left to the builder

- Where the new projects go: `src/Stabilt.TellusTalk.Client.AspNetCore` and its tests beside the others in `tests`.
- How the endpoint's tests host the app (`TestServer` or a `WebApplicationFactory`), and how the emulator's container
  reaches a receiver on the test host (Testcontainers' host port exposure, `host.testcontainers.internal`). The
  emulator sends a report at once with `POST /api/messages/{id}/reports` and `{"status":"delivered"}` or `"failed"`,
  which the emulator's client package has no method for.
- The decision records the repository keeps in `docs/decisions`: at least receiving reports in a package of its own,
  checked by a secret from the client's settings in `Authorization`.
- The README's section on reports, and the map in `AGENTS.md`.

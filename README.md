# TellusTalk Client

A typed .NET client for [TellusTalk](https://tellustalk.com/)'s
[K8 send API](https://k8api.tellustalk.com/openapi_docs/redoc.html). It sends SMS through `IHttpClientFactory`,
configured from the app's configuration or in code, and refuses what TellusTalk's specification forbids before anything
is sent. A second package receives TellusTalk's delivery reports in an ASP.NET Core app. Not affiliated with TellusTalk.

```shell
dotnet add package Stabilt.TellusTalk.Client
```

## Sending

```csharp
builder.Services.AddTellusTalkClient(builder.Configuration);
```

```json
{
  "TellusTalk": {
    "UserName": "…",
    "Password": "…"
  }
}
```

```csharp
public sealed class Reminders(TellusTalkClient tellusTalk)
{
    public Task<SendResult> RemindAsync(CancellationToken cancellationToken) => tellusTalk.SendAsync(
        new SmsMessage
        {
            To = ["+46701740605"],
            Text = "Your appointment is tomorrow at 08:30.",
            Sender = SmsSender.Name("Clinic"),
            ReportTo = [ReportAddress.Url(new Uri("https://app.example/sms/reports"))],
        },
        cancellationToken);
}
```

`BaseUrl` defaults to `https://k8api.tellustalk.com/`. Missing credentials, or a `BaseUrl` that is not an http(s) URL,
fail when the app starts, except while build-time OpenAPI generation runs the app without its secrets; they fail then
when a client is created. A change to the section reaches the next client created, so a rotated password needs no
restart.

`AddTellusTalkClient` reads its settings from configuration or takes them from code:

| Call | Reads |
|---|---|
| `AddTellusTalkClient(builder.Configuration)` | The `TellusTalk` section |
| `AddTellusTalkClient(builder.Configuration, "Sms")` | A section of the app's own name |
| `AddTellusTalkClient(builder.Configuration.GetSection("Sms"))` | The section passed |
| `AddTellusTalkClient(options => …)` | Nothing; the code sets every option |

A section passed as an `IConfiguration` rather than an `IConfigurationSection` is searched for `TellusTalk` beneath it.
An app that reads a section and sets an option in code as well adds `services.Configure<TellusTalkOptions>(…)` after
the call. Each form returns the client's `IHttpClientBuilder`, for handlers of the app's own.

The client adds no retries. A send that failed in transit may still have reached TellusTalk, and sending it again would
send the SMS twice; an app that adds a resilience handler should leave `POST` out of its retries.

## The message

| Property | TellusTalk's field | Rule |
|---|---|---|
| `To` | `to` | One or more numbers, `+` followed by digits; each is sent as `sms:<number>` |
| `Text` | `text` | |
| `Sender` | `sms_originator_source` and the fields that go with it | `SmsSender.Name("Clinic")`, at most 11 characters; `SmsSender.Subject("Clinic")`, a name sent as the `subject`, which sets the message apart from TellusTalk's duplicates and starts its report's text; `SmsSender.Number("+46…")`; `SmsSender.ReplyNumber(replyTo)`, a number of TellusTalk's that takes replies, which go to `replyTo` (`reply_to`) in `ReportTo`'s forms. Left out, the account's own sender |
| `ScheduledSendTime` | `scheduled_send_time` | Sent with its offset, so it never depends on how TellusTalk reads a time without one |
| `ReportTo` | `report_to` | `ReportAddress.Url(url)`, optionally with headers for the report request; `ReportAddress.Email(…)`; `ReportAddress.Sms(…)` |
| `MaxParts` | `sms_max_parts` | At least 1 |
| `Flash` | `sms_flash` | |
| `ValidityTime` | `sms_validity_time` | At least a second; sent in whole seconds |
| `CharacterSet` | `sms_character_set` | `Optimize`, `Gsm` or `Unicode`; `Gsm` is the caller's promise that the text is in the GSM alphabet |
| `BillingId` | `billing_id` | Letters a–z and digits |
| `Echo` | `echo` | Any JSON; TellusTalk carries it back in the delivery report |

A rule that is broken throws `ArgumentException` where the value is set. A refusal from TellusTalk, or an answer that is
not a send's answer, throws `TellusTalkException` with TellusTalk's status code and text. Its `Accepted` says TellusTalk
answered with success even so: the SMS may go out, and the message must not be sent again.

`SendResult` holds TellusTalk's ids for the send, which its delivery reports carry too, `LogUuid` only for a send to
one recipient. `InvalidRecipients`, `InvalidReportTo` and `InvalidReplyTo` list the addresses TellusTalk could not use,
each with TellusTalk's reason; the rest of the message is sent all the same, so they throw nothing. The client refuses
what TellusTalk was seen to list before it is sent, so the lists hold what TellusTalk lists besides.

The client sends SMS only. TellusTalk's other channels, `html` and `attachments` on an SMS, binary SMS with `sms_udh`,
and the deprecated `sms_format` and `sms_header` have no properties.

## Delivery reports

TellusTalk posts a delivery report to each URL in a message's `ReportTo`. `Stabilt.TellusTalk.Client.AspNetCore` maps
the endpoint that receives them: it checks that a report carries the report secret, reads it, and hands it to a handler
of the app's own.

```shell
dotnet add package Stabilt.TellusTalk.Client.AspNetCore
```

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
```

```csharp
public sealed class SmsReportHandler(IReportQueue queue) : ITellusTalkReportHandler
{
    public Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken) =>
        queue.EnqueueAsync(report, cancellationToken);
}
```

```csharp
await tellusTalk.SendAsync(
    new SmsMessage
    {
        To = ["+46701740605"],
        Text = "Your appointment is tomorrow at 08:30.",
        ReportTo = [tellusTalk.ReportAddress],
        Echo = JsonSerializer.SerializeToElement(new { appointment = 4711 }),
    },
    cancellationToken);
```

`TellusTalkClient.ReportAddress` is `Reports:Url` carrying the secret as `Authorization: Report <secret>`, which
TellusTalk sends back with each report; the secret and the address come from the same settings, so they cannot
disagree. `Reports` is optional, but when it is there `Url`, an absolute http(s) URL, and `Secret` are required, and
`AddTellusTalkReports` requires `Reports`; a wrong setting fails when the app starts. `Reports:Url` is where TellusTalk
reaches the app, and the pattern is the route the app receives it on; behind a proxy that rewrites paths they differ.

`MapTellusTalkReports` returns the endpoint's builder, for a route group, rate limiting or `ExcludeFromDescription`. The
endpoint allows anonymous requests, so an app's fallback authorization policy does not turn TellusTalk away, and checks
the secret itself; nothing turns that check off. It answers:

| Answer | When |
|---|---|
| `204` | The handler returned |
| `401` | `Authorization` is missing, of another scheme than `Report`, or carries neither the secret nor a previous one; the handler is not called |
| `400` | The body is not JSON, or has no `log_status` |
| `500` | The handler threw; the app's own error handling answers |

What TellusTalk does with an answer other than `200`, and whether it ever sends a report again, is not known. A handler
should hand the report on quickly, such as to a queue, and take the same report twice without harm.

| `Status` | TellusTalk's `log_status` |
|---|---|
| `Delivered` | `txtDELIVERED` |
| `Failed` | `txtDELIVERY_FAILED`; `FailedReason` says why |
| `Blocked` | `txtBLOCKED`: TellusTalk held the send back, such as a duplicate of one sent within five minutes; reported at once, without a `ReportTime` |
| `Unknown` | Any other; `StatusText` and `StatusCode` hold TellusTalk's own |

`To` is the number as the message gave it. A report is tied to its message by `Echo`, which comes back as it was sent
and works for any number of recipients, or by `LogUuid` against `SendResult.LogUuid`, for a send to one recipient only.
A report leaves out the message's text: the app has it already.

To rotate the secret, set the new one as `Secret` and move the old one to `PreviousSecrets`
(`"PreviousSecrets": ["…"]`). A report carries the secret its message was sent with, possibly days later, so keep the
old one listed until those reports have come. A change to the section takes effect without a restart.

An app without ASP.NET Core reads a report with `JsonSerializer.Deserialize<DeliveryReport>(body)` and checks the
request's `Authorization` with `ReportAuthorization.IsValid(authorization, reports)`.

## Against the emulator

[TellusTalk Emulator](https://github.com/stabiltdev/TellusTalk.Emulator) stands in for TellusTalk in development and
tests; give the client its address instead of TellusTalk's:

```json
{
  "TellusTalk": {
    "BaseUrl": "http://localhost:8080/",
    "UserName": "dev",
    "Password": "dev"
  }
}
```

The emulator takes any user name and password, but the client still needs both to start. From an Aspire AppHost,
`Stabilt.TellusTalk.Emulator.Aspire`'s `WithReference` sets `TellusTalk__BaseUrl` for the app, or the setting its second
argument names, such as `Sms__BaseUrl` for a section of the app's own name; the credentials still come from the app's
own configuration.

## Building

```shell
dotnet build Stabilt.TellusTalk.Client.slnx -c Release
dotnet test --solution Stabilt.TellusTalk.Client.slnx
```

The tests start the emulator's image with Testcontainers, so they need Docker.

Why the client works as it does is recorded in [docs/decisions](docs/decisions/README.md), and how its code and tests are
written in [docs/conventions.md](docs/conventions.md). [CONTRIBUTING.md](CONTRIBUTING.md) says how to report a problem
or propose a change.

## Licence

MIT; see [LICENSE](LICENSE).

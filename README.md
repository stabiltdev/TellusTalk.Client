# TellusTalk Client

A typed .NET client for [TellusTalk](https://tellustalk.com/)'s
[K8 send API](https://k8api.tellustalk.com/openapi_docs/redoc.html). It sends SMS through `IHttpClientFactory`,
configured from the app's configuration or in code, and refuses what TellusTalk's specification forbids before anything
is sent. Not affiliated with TellusTalk.

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
            Sender = Sender.Name("Clinic"),
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
| `Sender` | `sms_originator_source` and its value | `Sender.Name("Clinic")`, at most 11 characters; `Sender.Subject("Clinic")`, a name sent as the `subject`, which sets the message apart from TellusTalk's duplicates and starts its report's text; `Sender.Number("+46…")`; `Sender.ReplyNumber()`, a number of TellusTalk's that takes replies. Left out, the account's own sender |
| `ScheduledSendTime` | `scheduled_send_time` | Sent with its offset, so it never depends on how TellusTalk reads a time without one |
| `ReportTo` | `report_to` | `ReportAddress.Url(url)`, optionally with headers for the report request; `ReportAddress.Email(…)`; `ReportAddress.Sms(…)` |
| `ReplyTo` | `reply_to` | Where replies to a `Sender.ReplyNumber()` go, in the same forms |
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

---
status: accepted
date: 2026-11-27
deciders: [Peter Hedberg]
supersedes: [ADR 20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200](20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200.md)
---

# ADR 20261127-receive-delivery-reports-with-the-report-settings-alone: Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization, answered 200, and configured by the report settings alone

## Context and Problem Statement

An app that received reports while another app sent them had to hold K8's user name and password, and an absolute
`Reports:Url`, though it used neither: the endpoint read the secret from the client's settings, whose validation
requires the credentials, and `MapTellusTalkReports` required `AddTellusTalkClient`. K8 posts a delivery report as JSON
to each URL in a message's `report_to`, once, and logs a report answered anything but `200`, or not within 60 seconds,
as a failed delivery. It signs nothing: the only sign that a report is genuine is a header the app put in the report
address, which K8 sends back unchanged. The endpoint has to be reachable from the internet, so one without a check lets
anyone who finds it mark an SMS delivered or failed.

## Considered Options

- The app adds the report address to each message itself.
- The client adds the report address to every message when `Reports` is set.
- The route given to the endpoint apart from `Reports:Url`.
- The route taken from the path of `Reports:Url`, with a route given where the two differ.
- Only the app's public origin in the settings, and the address built from the mapped endpoint's route.
- The address built from the current request.
- Reading only: `DeliveryReport` in the main package, and each app writes its endpoint and its check of the secret.
- The secret in a header of the package's own, or as a `Bearer` token, rather than in `Authorization` under a scheme of
  its own.
- The secret checked by an authentication handler registered in the app, rather than by the endpoint.
- A report taken answered `204`, as webhooks commonly answer, or `200`.
- The report settings registered by `AddTellusTalkReports` from the section the client reads.
- One settings class whose credentials are required only where the client is registered.

## Decision Outcome

**Every message carries the report address of `Reports:Url`, and the endpoint `Stabilt.TellusTalk.Client.AspNetCore`
maps takes a report only when its `Authorization` is `Report <secret>`, answers it `200`, and reads the secret from the
report settings alone.** `TellusTalkReportOptions` holds `Url`, `Secret` and `PreviousSecrets`. As
`TellusTalkOptions.Reports` it is the client's: when it is set, `Url` and `Secret` are required, and
`TellusTalkClient.SendAsync` opens `report_to` with `Url` carrying that header, before the message's own `ReportTo`.
`AddTellusTalkReports<THandler>` takes the arguments `AddTellusTalkClient` takes, the configuration and a section name,
the section itself, or code; it registers the app's `ITellusTalkReportHandler` and binds the section's `Reports` into
report settings of its own, requiring only `Secret`, with `Url` checked when it is set. `MapTellusTalkReports()` maps
the endpoint at the path of `Reports:Url`, and `MapTellusTalkReports(pattern)` where the app receives on another route;
neither needs the client. `DeliveryReport` and `ReportAuthorization` stay in the main package.

The same keys configure one app that sends and receives and two apps that split the work, so the settings of an app
that does both are the two apps' settings together, and a receiving app holds no sending credentials it never uses. One
settings class with the credentials required only where the client is registered was rejected: its rules would depend
on which registration ran, which nothing in the class shows, and the receiving app would need its section bound all
the same.

The address is stated once, and a message cannot leave it out. The public address cannot come from the app itself: a
scheduled send has no request, and `Host` is the caller's to set, so a send triggered with a forged `Host` would have
TellusTalk post the secret elsewhere. Building the address from the public origin and the mapped route, through
`LinkGenerator`, was rejected because the address would exist only in an app that maps the endpoint. The path of
`Reports:Url` is the route where the app is reached directly; behind a proxy that rewrites paths, under `UsePathBase` or
in a route group it is not, so the app gives the route there.

The check is written once, and the main package stays free of ASP.NET Core: an app without it still reads a report with
`JsonSerializer` and checks it with `ReportAuthorization`. `Authorization` is what logging tools treat as a credential
and leave out; the scheme is `Report` because an app's JWT bearer authentication reads every `Bearer` value as a token,
fails on the secret and logs it. The secret cannot be left out, and nothing turns the check off: a way to turn it off
can be added later without breaking an app, while making the secret required after it had been optional would break
every app without one. `PreviousSecrets` keeps a rotated secret valid for the reports of messages sent with it, and the
settings are read through `IOptionsMonitor`, so a rotation needs no restart. The endpoint allows anonymous requests and
checks the secret in an endpoint filter. An authentication handler would register authentication, and possibly a
default scheme, for the whole app to check one endpoint's header, and a filter runs after the app's authorization, where
a fallback policy would turn TellusTalk away first.

The endpoint answers `200` without a body for a report taken, `401` for a missing or wrong secret and `400` for a body
that is not a report, and leaves a handler's exception to the app's error handling (`500`). `200` is the answer K8 logs
as delivered; `204`, the common answer of a webhook, it logs as failed, which would hide the real failures among false
ones. An error is answered rather than `200` regardless, so a lost report shows in the app's telemetry and in K8's log.

## Consequences

- **Good:** an app gets checked reports for every message from its settings and a few lines of code, an app that only
  receives them needs the secret and its route alone, and K8's log tells a report taken from one lost.
- **Bad:** one more package to keep, released with the main one at one version. The secret is stored in each message's
  `report_to` at TellusTalk, and travels in the clear to a `Reports:Url` that is plain http.
- **Bad:** an app that sends and receives from code gives the secret to both calls. With two apps, a rotation reaches
  the receiving app first, as it reaches every instance that takes reports first.
- **Bad:** behind a proxy that rewrites paths, under `UsePathBase` or in a route group the route is given besides the
  URL. The path is read when the endpoint is mapped, so a changed path needs a restart, and `Reports:Url` has to be
  readable wherever the app maps its endpoints without a route, build-time OpenAPI generation included. A message cannot
  yet leave the report address out.
- **Bad:** K8 sends no report again, so a report whose handler threw, or that is not answered within 60 seconds, is
  lost; a `302` in front of the endpoint loses it too, since K8 follows one with a `GET` without the body. An
  authorization policy the app puts on the endpoint is not applied, since it allows anonymous requests.

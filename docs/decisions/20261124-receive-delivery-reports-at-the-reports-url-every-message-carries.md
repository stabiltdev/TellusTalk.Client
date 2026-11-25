---
status: superseded by [ADR 20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200](20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200.md)
date: 2026-11-24
deciders: [Peter Hedberg]
supersedes: [ADR 20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret](20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md)
---

# ADR 20261124-receive-delivery-reports-at-the-reports-url-every-message-carries: Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization

## Context and Problem Statement

Under the superseded record an app stated where TellusTalk reaches it twice, in `Reports:Url` and in the pattern of
`MapTellusTalkReports(pattern)`, and put `TellusTalkClient.ReportAddress` in each message itself; a message without it
lost its reports without a trace. TellusTalk's K8 posts a delivery report as JSON to each URL in a message's
`report_to` and signs nothing: the only sign that a report is genuine is a header the app put in the report address. The
endpoint has to be reachable from the internet, so one without a check lets anyone who finds it mark an SMS delivered or
failed.

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

## Decision Outcome

**Every message carries the report address of `Reports:Url`, and the endpoint `Stabilt.TellusTalk.Client.AspNetCore`
maps takes a report only when its `Authorization` is `Report <secret>`.** `TellusTalkOptions.Reports` holds `Url`,
`Secret` and `PreviousSecrets`. When it is set, `TellusTalkClient.SendAsync` opens `report_to` with `Url` carrying that
header, before the message's own `ReportTo`. `AddTellusTalkReports<THandler>()` registers the app's
`ITellusTalkReportHandler`; `MapTellusTalkReports()` maps the endpoint at the path of `Reports:Url`, and
`MapTellusTalkReports(pattern)` where the app receives on another route. `DeliveryReport` and `ReportAuthorization` stay
in the main package.

The address is stated once, and a message cannot leave it out. The public address cannot come from the app itself: a
scheduled send has no request, and `Host` is the caller's to set, so a send triggered with a forged `Host` would have
TellusTalk post the secret elsewhere. Building the address from the public origin and the mapped route, through
`LinkGenerator`, was rejected because the address would exist only in an app that maps the endpoint. The path of
`Reports:Url` is the route where the app is reached directly; behind a proxy that rewrites paths, under `UsePathBase` or
in a route group it is not, so the app gives the route there.

The check is written once, and the main package stays free of ASP.NET Core: an app without it still reads a report with
`JsonSerializer` and checks it with `ReportAuthorization`. The secret and the address come from the same settings, so
they cannot disagree. `Authorization` is what logging tools treat as a credential and leave out; the scheme is `Report`
because an app's JWT bearer authentication reads every `Bearer` value as a token, fails on the secret and logs it. The
secret cannot be left out, and nothing turns the check off: a way to turn it off can be added later without breaking an
app, while making the secret required after it had been optional would break every app without one. `PreviousSecrets`
keeps a rotated secret valid for the reports of messages sent with it, and the settings are read through
`IOptionsMonitor`, so a rotation needs no restart. The endpoint answers `204` for a report taken, `401` for a missing or
wrong secret and `400` for a body that is not a report, and leaves a handler's exception to the app's error handling
(`500`). What K8 does with an answer other than `200` is not known; these follow how webhooks are commonly answered, and
they show a lost report in the app's telemetry where answering `204` regardless would hide it.

## Consequences

- **Good:** an app gets checked reports for every message from its settings and two lines of code.
- **Bad:** one more package to keep, released with the main one at one version. The secret is stored in each message's
  `report_to` at TellusTalk, and travels in the clear to a `Reports:Url` that is plain http.
- **Bad:** behind a proxy that rewrites paths, under `UsePathBase` or in a route group the route is given besides the
  URL. The path is read when the endpoint is mapped, so a changed path needs a restart, and `Reports:Url` has to be
  readable wherever the app maps its endpoints, build-time OpenAPI generation included. A message cannot yet leave the
  report address out.
- **Bad:** whether K8 sends a report again after a `400`, `401` or `500` is not known, so a handler has to take the same
  report twice without harm.
- **Follow-up:** a send to K8 whose report address carries `Authorization: Report <secret>` confirms that the value
  arrives unchanged. A probe of K8 against a receiver that answers `500`, `401` or nothing shows whether it sends again;
  the codes are reconsidered if K8 does something harmful with them, such as turning the address off. An app that only
  receives reports needs K8's credentials today, since the endpoint reads the client's settings.

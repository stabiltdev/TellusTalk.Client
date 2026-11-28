---
status: superseded by [ADR 20261127-receive-delivery-reports-with-the-report-settings-alone](20261127-receive-delivery-reports-with-the-report-settings-alone.md)
date: 2026-11-25
deciders: [Peter Hedberg]
supersedes: [ADR 20261124-receive-delivery-reports-at-the-reports-url-every-message-carries](20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md)
---

# ADR 20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200: Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization and answered 200

## Context and Problem Statement

The superseded record answered a report taken with `204` without knowing how TellusTalk's K8 treats an answer other
than `200`. Probes of K8 have since shown that it logs a report answered `204` as a failed delivery, as it does one
answered `401` or `500` or not at all within 60 seconds, and that it posts a report once and never again. K8 posts a
delivery report as JSON to each URL in a message's `report_to` and signs nothing: the only sign that a report is genuine
is a header the app put in the report address, which K8 sends back unchanged. The endpoint has to be reachable from the
internet, so one without a check lets anyone who finds it mark an SMS delivered or failed.

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

## Decision Outcome

**Every message carries the report address of `Reports:Url`, and the endpoint `Stabilt.TellusTalk.Client.AspNetCore`
maps takes a report only when its `Authorization` is `Report <secret>`, and answers it `200`.**
`TellusTalkOptions.Reports` holds `Url`, `Secret` and `PreviousSecrets`. When it is set, `TellusTalkClient.SendAsync`
opens `report_to` with `Url` carrying that header, before the message's own `ReportTo`. `AddTellusTalkReports<THandler>()`
registers the app's `ITellusTalkReportHandler`; `MapTellusTalkReports()` maps the endpoint at the path of `Reports:Url`,
and `MapTellusTalkReports(pattern)` where the app receives on another route. `DeliveryReport` and `ReportAuthorization`
stay in the main package.

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
`IOptionsMonitor`, so a rotation needs no restart. The endpoint allows anonymous requests and checks the secret in an
endpoint filter. An authentication handler would register authentication, and possibly a default scheme, for the whole
app to check one endpoint's header, and a filter runs after the app's authorization, where a fallback policy would turn
TellusTalk away first.

The endpoint answers `200` without a body for a report taken, `401` for a missing or wrong secret and `400` for a body
that is not a report, and leaves a handler's exception to the app's error handling (`500`). `200` is the answer K8 logs
as delivered; `204`, the common answer of a webhook, it logs as failed, which would hide the real failures among false
ones. An error is answered rather than `200` regardless, so a lost report shows in the app's telemetry and in K8's log.

## Consequences

- **Good:** an app gets checked reports for every message from its settings and two lines of code, and K8's log tells a
  report taken from one lost.
- **Bad:** one more package to keep, released with the main one at one version. The secret is stored in each message's
  `report_to` at TellusTalk, and travels in the clear to a `Reports:Url` that is plain http.
- **Bad:** behind a proxy that rewrites paths, under `UsePathBase` or in a route group the route is given besides the
  URL. The path is read when the endpoint is mapped, so a changed path needs a restart, and `Reports:Url` has to be
  readable wherever the app maps its endpoints, build-time OpenAPI generation included. A message cannot yet leave the
  report address out.
- **Bad:** K8 sends no report again, so a report whose handler threw, or that is not answered within 60 seconds, is
  lost; a `302` in front of the endpoint loses it too, since K8 follows one with a `GET` without the body. An
  authorization policy the app puts on the endpoint is not applied, since it allows anonymous requests.
- **Follow-up:** an app that only receives reports needs K8's credentials today, since the endpoint reads the client's
  settings.

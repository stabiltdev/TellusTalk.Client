---
status: superseded by [ADR 20261124-receive-delivery-reports-at-the-reports-url-every-message-carries](20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md)
date: 2026-11-23
deciders: [Peter Hedberg]
---

# ADR 20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret: Receive delivery reports at an endpoint of a package of its own, checked by a secret in Authorization

## Context and Problem Statement

TellusTalk's K8 posts a delivery report as JSON to each URL in a message's `report_to`. It signs nothing: the only sign
that a report is genuine is a header the app itself put in the report address. The endpoint has to be reachable from the
internet, so one without a check lets anyone who finds it mark an SMS delivered or failed, and an app that writes its
own check is one mistake away from that.

## Considered Options

- Reading only: `DeliveryReport` in the main package, and each app writes its endpoint and its check of the secret.
- Reading in the main package, and receiving in a package of its own that maps the endpoint and checks the secret.
- The secret in the settings for the check only, with the app building the report address with the same secret.
- The secret, and the address carrying it, both from the client's settings.
- The secret in a header of the package's own, or as a `Bearer` token, rather than in `Authorization` under a scheme of
  its own.

## Decision Outcome

**A delivery report is received at the endpoint `Stabilt.TellusTalk.Client.AspNetCore` maps, and is taken only when its
`Authorization` is `Report <secret>` with a secret from the client's `Reports` settings.** `TellusTalkOptions.Reports`
holds `Url`, `Secret` and `PreviousSecrets`; `TellusTalkClient.ReportAddress` is the `Url` carrying that header;
`AddTellusTalkReports<THandler>()` registers the app's `ITellusTalkReportHandler`, and `MapTellusTalkReports(pattern)`
maps the endpoint. `DeliveryReport` and `ReportAuthorization` stay in the main package.

The check is written once, and the main package stays free of ASP.NET Core: an app without it still reads a report with
`JsonSerializer` and checks it with `ReportAuthorization`. The secret and its header live in one place, so the address
an app sends and the check it runs cannot disagree. `Authorization` is what logging tools, ASP.NET Core's HTTP logging
among them, treat as a credential and leave out; the scheme is `Report` because an app's JWT bearer authentication reads
every `Bearer` value as a token, fails on the secret and logs it.

The secret cannot be left out, and nothing turns the check off: a way to turn it off can be added later without breaking
an app, while making the secret required after it had been optional would break every app without one. A report carries
the secret its message was sent with, possibly days later, so `PreviousSecrets` keeps a rotated secret valid, and the
settings are read through `IOptionsMonitor` so a rotation needs no restart.

The endpoint answers `204` for a report taken, `401` for a missing or wrong secret and `400` for a body that is not a
report, and leaves a handler's exception to the app's error handling (`500`). What K8 does with an answer other than
`200` is not known; these follow how webhooks are commonly answered, and they show a lost report in the app's telemetry
where answering `204` regardless would hide it.

## Consequences

- **Good:** an ASP.NET Core app gets a checked endpoint in a few lines, and handles each report in a class that takes
  its services through its constructor.
- **Bad:** one more package to keep, released with the main one at one version. The secret is stored in each message's
  `report_to` at TellusTalk, and travels in the clear to a `Reports:Url` that is plain http.
- **Bad:** whether K8 sends a report again after a `400`, `401` or `500` is not known, so a handler has to take the same
  report twice without harm.
- **Follow-up:** a send to K8 whose report address carries `Authorization: Report <secret>` confirms that the value
  arrives unchanged, before the first release. A probe of K8 against a receiver that answers `500`, `401` or nothing
  shows whether it sends again; the codes are reconsidered if K8 does something harmful with them, such as turning the
  address off.

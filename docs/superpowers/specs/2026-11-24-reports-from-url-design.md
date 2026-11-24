# Report address and route from `Reports:Url`

## Intent

An app that takes delivery reports says where TellusTalk reaches it twice: `Reports:Url` in the settings, and the path
again in `MapTellusTalkReports(pattern)`. It also has to put `ReportTo = [tellusTalk.ReportAddress]` on every message
that should be reported on, and a message where that is forgotten loses its reports without a trace. `Reports:Url`
should be enough: the endpoint takes its route from it, and every message carries it.

## Goals

- `app.MapTellusTalkReports()` maps the endpoint at `Reports:Url`'s path.
- `MapTellusTalkReports(pattern)` stays, for an app whose route is not the public path: behind a proxy that rewrites
  paths, such as Azure Front Door, under `UsePathBase`, or in a route group.
- When `Reports` is set, the client puts the report address, `Reports:Url` carrying `Authorization: Report <secret>`, in
  every message's `report_to`. `SmsMessage.ReportTo` holds only addresses beyond it, such as an email address.
- `TellusTalkClient.ReportAddress` is removed.

## Non-goals

- An app that only receives reports, without K8's user name and password. Today the endpoint needs
  `AddTellusTalkClient` and its credentials; that is the next change.
- Turning the automatic address off for one message. It can be added later without breaking an app.

## Anti-goals

- The public address taken from the current request or the server's own addresses. A scheduled send has no request,
  a container's server address is not where the world reaches it, and `Host` is the caller's to set: a send triggered
  with a forged `Host` would have TellusTalk post the secret to the attacker.

## Constraints

- 0.1.0 is released, but nobody uses it yet, so breaking it is allowed; the change is marked `!` with a
  `BREAKING CHANGE:` footer.
- Build-time OpenAPI generation starts the app without its secrets, which is why `AddTellusTalkClient` skips
  `ValidateOnStart` there. `MapTellusTalkReports()` therefore reads `Reports:Url` without validating the other settings.
- An accepted decision record is not edited. A new record supersedes
  ADR 20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret, which names `TellusTalkClient.ReportAddress`
  and `MapTellusTalkReports(pattern)` as the decision.

## Approaches

- **A. `Reports:Url` holds the whole address, and the route is its path.** Simplest, the client package needs nothing
  from the ASP.NET Core package, and an app that only sends gets the address too. A route that differs from the public
  path is given to `MapTellusTalkReports(pattern)`, so behind a rewriting proxy or in a route group the path is stated
  twice, as it has to be.
- **B. The settings hold only the app's public origin, and the address is built from the mapped endpoint's route**
  through `LinkGenerator`. Route groups and proxy prefixes work without stating anything twice, but the address exists
  only in an app that maps the endpoint, and the client package needs a hook the ASP.NET Core package fills.
- **C. The address from the current request.** Rejected, for the reasons under anti-goals.

The user chose A: the address is stated once, in the setting the app already has, and the two overloads cover a
rewriting proxy and an app that sends while another receives.

## Design

### The route

- `MapTellusTalkReports(this IEndpointRouteBuilder endpoints)` maps the endpoint at `Reports:Url`'s path and is
  otherwise the pattern overload: `POST`, anonymous, and the same check that `AddTellusTalkReports` and
  `AddTellusTalkClient` are registered.
- It throws `InvalidOperationException` when:
  - `endpoints` is a `RouteGroupBuilder`. The group's prefix would go in front of a path that is already whole, and
    every report would meet `404` without anyone noticing. In a group, the app gives the route.
  - `Reports` or `Reports:Url` is missing, or the URL is not absolute. The message says to set it or to give the route.
- The path is read once, when the endpoint is mapped. A changed path needs a restart; a changed host or secret does not.
- The query of `Reports:Url` is not part of the route; TellusTalk posts to the URL with it, and routing ignores it.

### The address

- When `Reports` is set, `TellusTalkClient.SendAsync` sends `report_to` with the report address first, then the
  message's `ReportTo`. Without `Reports`, it sends the message's `ReportTo` alone, as now.
- The address is read from the current settings at each send, so a rotated secret reaches the next message.
- `ReportAddress.Url` and `ReportAuthorization` stay public: an app without ASP.NET Core still receives and checks
  reports itself, and the client adds the address for it as for any other app.

### Documentation

- The README's delivery reports: the settings, `app.MapTellusTalkReports()`, a message without `ReportTo`, the pattern
  overload for a rewriting proxy or a route group, `ReportTo` for addresses beyond the report endpoint, and that a
  changed path needs a restart.
- The XML documentation of the members that change.
- A new decision record superseding ADR 20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret. It
  restates what still holds, the endpoint of a package of its own and the secret in `Authorization`, with the address
  and route now from `Reports:Url`.

### Tests

- The client: with `Reports`, the request's `report_to` holds the address with its header, before the message's own;
  without `Reports`, only the message's.
- The endpoint: `MapTellusTalkReports()` takes a report at the URL's path; it is refused in a route group and without
  `Reports:Url`. The pattern overload's tests stand.
- The emulator: a message sent without `ReportTo` still has its report reach the handler.

## Left to the builder

- How `Reports:Url` is read without validating the other settings.
- Whether the path is unescaped before it becomes the route.
- The new decision record's identity and wording, and the tests' names and places.

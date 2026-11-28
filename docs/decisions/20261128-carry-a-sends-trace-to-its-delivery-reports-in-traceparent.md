---
status: accepted
date: 2026-11-28
deciders: [Peter Hedberg]
---

# ADR 20261128-carry-a-sends-trace-to-its-delivery-reports-in-traceparent: Carry a send's trace to its delivery reports in traceparent

## Context and Problem Statement

A delivery report reaches the app as a request of its own, possibly days after the send, in a trace of its own; joining
the two in the logs takes the app logging `JobUuid` on both sides. TellusTalk's K8 stores each report address with its
message and puts every header of a URL address's settings on the report's request, as written, so a value the client
writes into the address comes back on the report. A probe of K8 on 2026-11-28 showed a `traceparent` in the settings
arriving on the report as written, and no trace context on a report whose address named none.

## Considered Options

- `traceparent` in the report address, making the report's request a child of the send's span.
- A header of the client's own, and a span link the endpoint adds to the report's request.
- The trace context in the message's `Echo`.
- The context written through `DistributedContextPropagator` rather than from the activity's id.
- `tracestate` and `baggage` carried besides `traceparent`.
- A setting to turn it off.

## Decision Outcome

**When `Reports` is set and the send runs within an activity in W3C format, the report address carries the activity's
id as `traceparent`, and nothing else of the trace.** `TellusTalkClient.SendAsync` reads `Activity.Current` and puts
`traceparent` after `Authorization` in the address of `Reports:Url`; without an activity, or with one in the
hierarchical format, the address carries `Authorization` alone. The addresses of a message's `ReportTo` are sent as
they are.

`traceparent` is the standard header, which the receiver's hosting reads as the request's parent, so the receiving side
needs no code, in `Stabilt.TellusTalk.Client.AspNetCore` or anywhere else. A span link is what tracing guidance usually
prefers for work decoupled in time, but it keeps the report in a trace of its own, joined to the send only in a backend
that shows links, which defeats joining the logs. `Echo` is the app's, and comes back as it was sent. `baggage` is the
app's data and would be stored at TellusTalk with the message, which is why the value is the activity's id rather than
what `DistributedContextPropagator` writes; `tracestate` is vendor data that may hold spaces, which K8 is not known to
carry intact. A way to turn it off can be added later without breaking an app.

## Consequences

- **Good:** the handler's logs carry the send's `TraceId` without a line of code, in any receiver that reads W3C trace
  context.
- **Bad:** a trace spans the days a report can take, and a collector's tail sampling has decided before the report
  comes. The parent is the app's span current at the send, not the HTTP call to K8, which `HttpClient` creates later.
  An app cannot turn it off, and one that upgrades sees its report requests join the trace of their send.
- **Bad:** the report's request takes the send's sampled flag. Where the send's activity was not recorded, as one
  created only for logging is not, a parent-based sampler in the receiver, OpenTelemetry's default, drops the report's
  span, where a request without `traceparent` would be sampled as a root of its own; the logs still carry the trace
  id. A receiver that wants those spans has its sampler record requests whose remote parent was not recorded.

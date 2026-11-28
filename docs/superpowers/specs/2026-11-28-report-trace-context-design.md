# A send's trace carried to its delivery reports

## Intent

A delivery report reaches the app as a request of its own, possibly days after the send, in a trace of its own. Tying
the two together in the logs today takes the app logging `JobUuid` on both sides. The report's request should belong to
the send's trace, so every log line the handler writes carries the send's `TraceId` without the app doing anything.

## Goals

- When `Reports` is set and the send runs within an `Activity` in W3C format, the report address of `Reports:Url`
  carries `traceparent` with that activity's trace id, span id and flags, beside `Authorization`.
- An app's receiving side needs no code: ASP.NET Core's hosting reads `traceparent` and makes the report's request a
  child of the send's span. A receiver without `Stabilt.TellusTalk.Client.AspNetCore` gets the same from its own stack.

## Non-goals

- A setting to turn it off. It can be added later without breaking an app.
- `tracestate`. It holds vendor data that may contain spaces, which K8 is not known to carry back intact.
- An `ActivitySource` of the client's own, with a span for the send or the report, or tags such as `JobUuid`.
- A span link in place of a parent.
- Trace context on the addresses an app puts in `SmsMessage.ReportTo` itself.

## Anti-goals

- `baggage`, or anything else the app defines, stored at TellusTalk in `report_to`.
- Touching `Echo`, which is the app's and comes back as it was sent.

## Constraints

- A URL address carries its headers as JSON between its scheme and `://`, which `ReportAddress.Url(url, headers)` builds:
  `https:{"headers":{"Authorization":"Report s3cret","traceparent":"00-…-…-01"}}://app.invalid/sms/reports`.
- K8 stores the address with the message and, when it posts the report, puts every header of the settings on the
  request beside its own: a probe on 2026-11-24 with `Authorization`, `Report-Secret` and `X-Probe-MiXeD` got all three
  back with their values as written (the emulator's `docs/k8-behaviour.md`). `traceparent` itself has not been sent
  through K8. Its sender is Python's aiohttp, which would set a `traceparent` of its own only if K8 had tracing
  instrumentation; if K8 replaced or dropped ours, nothing would break and nothing would be joined.
- A header value is visible ASCII without spaces, which `traceparent`'s hex and dashes are.
- K8's duplicate check is unaffected: the emulator's model of it (`MessageStore.FindOriginal`) keys on the user, the
  recipient, the subject and the text, not on `report_to`.
- `Activity` is in the shared framework, so no package is added.
- 0.3.0 is not yet tagged. The change is a `feat`, not breaking, but an app that upgrades sees its report requests join
  the send's trace, which the README and the pull request say.

## Approaches

1. **`traceparent` in the report address, the report's request a child of the send's span.** The standard header, read
   by every tracing stack, so the receiving side needs nothing.
2. **A header of the client's own, and a span link added by the endpoint.** What tracing guidance usually prefers for
   work decoupled in time, but the report's request keeps a trace id of its own, so its logs join the send's only in a
   backend that shows links. It also needs code in the endpoint and works only there.
3. **The trace context in `Echo`.** Rejected: `Echo` is the app's.

The user chose 1: "Det låter superbra med `traceparent` rakt av. Då använder vi något som redan är standardiserat." Asked
whether it should be possible to turn it off, the recommendation was no setting, since one can be added later without
breaking an app; the user went ahead with the spec on it.

## Design

- `TellusTalkClient.SendAsync` builds the report address with `traceparent` when `Activity.Current` is set and its
  `IdFormat` is W3C. The value is the activity's W3C id, `00-<trace-id>-<span-id>-<flags>`, with the flags as the
  activity has them.
- The value is built from the activity itself, not through `DistributedContextPropagator`, which would add `baggage`.
- Without a current activity, or with one in the hierarchical format, the address carries `Authorization` alone.
- The parent is the app's span current when `SendAsync` is called, such as the request or job that sends; the span of
  the HTTP call to K8 is created later, inside `HttpClient`, and is not reachable from here.

### Documentation

- The README's paragraph on the report address: the address carries `traceparent` when the send runs within an
  activity, so the report's request joins the send's trace, possibly days later; the app's own `ReportTo` is untouched.
- The XML summary of `SendAsync`.
- A new decision record, standing alone rather than superseding one: the trace carried in `traceparent`, parent rather
  than link, no `tracestate` or `baggage`, no setting. Its follow-up: a send to K8 confirms that `traceparent` arrives as written.

### Tests

- Against `RecordingHandler`, comparing `report_to` as sent:
  - a send within an activity carries its id as `traceparent` after `Authorization`;
  - a send outside any activity carries `Authorization` alone;
  - a send within a hierarchical activity carries `Authorization` alone;
  - the message's own URL addresses carry no `traceparent`.
- Against the emulator, on Kestrel: the handler of a report runs in the send's trace, its request's parent being the
  span the send ran in. The emulator's own `HttpClient` passes on the trace of the call that has it post the report, so
  the test sends in one activity and has the report posted from another; a test where both share a trace would pass
  without the change. The app records the trace of the request the handler runs in, listening to ASP.NET Core's
  activities so the request has one whatever logging is set up.

## Left to the builder

- Whether the value is `Activity.Id` or composed from `Activity.Context`.
- The tests' names and places, and how the test app records the handler's trace.
- The decision record's identity and wording.

## Calls worth a look

1. **Only `traceparent`,** no `tracestate`.
2. **A hierarchical activity sends nothing,** rather than a value no tracing stack reads as a parent.
3. **The parent is the app's current span,** not the HTTP call to K8.
4. **No setting** to turn it off.

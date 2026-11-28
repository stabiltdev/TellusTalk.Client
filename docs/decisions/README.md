# Decisions

Short, dated records of decisions that shape the client: what was faced, what was considered, what was chosen, and what
it costs. Read them before changing something that looks odd; most of the odd things are on purpose.

## When to write one

**Write a record when a change involves:**

1. A new dependency, service or external integration
2. A change to what apps depend on: the public types, the settings, the package
3. A behaviour of TellusTalk's that the client follows, or deliberately does not
4. A pattern the code will now repeat
5. A choice made deliberately against the obvious option

**Do not write one for** version bumps, naming, formatting, anything one pull request can reverse without consequence,
or anything a record already covers. [conventions.md](../conventions.md) carries the conventions.

## Writing one

Copy [template.md](template.md) to `YYYYMMDD-short-title-with-dashes.md`, dated the day you create the file. The file
name without `.md` is the record's identity, and the heading opens with it:
`# ADR 20261108-retry-no-send: Retry no send`. Title the record with the decision itself, as the rule that holds, and
add its row to the table below in the same change.

Cite a record as a link with its identity as the text, such as
[ADR 20261108-retry-no-send](20261108-retry-no-send.md); in a code comment, without the link.

## Superseding

An accepted record is not edited. When a decision changes, write a new record whose front matter says
`supersedes: [ADR <identity>](<identity>.md)` and whose context opens with what changed; in the old record, change only
its `status` to `superseded by [ADR <identity>](<identity>.md)`, and update its row here.

## Records

| Record | Decision | Status |
|---|---|---|
| [20261108-refuse-what-tellustalks-specification-forbids](20261108-refuse-what-tellustalks-specification-forbids.md) | Refuse what TellusTalk's specification forbids where the message is built | accepted |
| [20261108-send-sms-only-with-one-type-per-choice](20261108-send-sms-only-with-one-type-per-choice.md) | Send SMS only, with one type for each choice TellusTalk spreads over several fields | accepted |
| [20261108-retry-no-send](20261108-retry-no-send.md) | Retry no send | accepted |
| [20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret](20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md) | Receive delivery reports at an endpoint of a package of its own, checked by a secret in Authorization | superseded by [ADR 20261124-receive-delivery-reports-at-the-reports-url-every-message-carries](20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md) |
| [20261124-receive-delivery-reports-at-the-reports-url-every-message-carries](20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md) | Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization | superseded by [ADR 20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200](20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200.md) |
| [20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200](20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200.md) | Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization and answered 200 | superseded by [ADR 20261127-receive-delivery-reports-with-the-report-settings-alone](20261127-receive-delivery-reports-with-the-report-settings-alone.md) |
| [20261127-receive-delivery-reports-with-the-report-settings-alone](20261127-receive-delivery-reports-with-the-report-settings-alone.md) | Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization, answered 200, and configured by the report settings alone | accepted |

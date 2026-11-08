---
status: accepted
date: 2026-11-08
deciders: [Peter Hedberg]
---

# ADR 20261108-send-sms-only-with-one-type-per-choice: Send SMS only, with one type for each choice TellusTalk spreads over several fields

## Context and Problem Statement

TellusTalk's send API takes one body for every channel it serves, SMS, MMS, e-mail, fax, voice, secure messages and
signatures, and spreads some single choices over several fields: the sender is `sms_originator_source` together with
`sms_originator_text` or `sms_originator_number`, and `sms_single_thread` means something only for one of the sources.
A report address with headers for its request is a JSON object written between a URL's scheme and its `://`. A client
that mirrors the body field for field lets an app build combinations that mean nothing.

## Considered Options

- Mirror every field of every channel as a property.
- Send SMS only, with the SMS fields whose meaning is known, and one type for each choice spread over several fields.

## Decision Outcome

**The client sends SMS only. `SmsMessage` has a property for each SMS field whose meaning is known; the sender is a
`Sender` and a report or reply address a `ReportAddress`, each built by a factory that writes TellusTalk's fields
together.** `Sender.Name`, `Sender.Number` and `Sender.ReplyNumber` stand for the three sources, and only
`ReplyNumber` takes the single-thread choice. `ReportAddress.Url` writes a URL's headers in TellusTalk's settings form.

The apps that use the client send SMS. `html` and `attachments` on an SMS, binary SMS with `sms_udh`, and the deprecated
`sms_format` and `sms_header` have no property: what TellusTalk does with them on an SMS is not known, and a property
would invite their use.

## Consequences

- **Good:** an app cannot build a sender without its value, or a single thread without a reply number; the report
  settings form is written in one place.
- **Bad:** another channel, or one of the omitted fields, needs a change to the client before an app can use it.
- **Follow-up:** none.

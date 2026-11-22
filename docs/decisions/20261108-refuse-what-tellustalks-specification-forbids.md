---
status: accepted
date: 2026-11-08
deciders: [Peter Hedberg]
---

# ADR 20261108-refuse-what-tellustalks-specification-forbids: Refuse what TellusTalk's specification forbids where the message is built

## Context and Problem Statement

TellusTalk's K8 platform accepts much that its specification forbids: probed in November 2026, it took a number with
spaces, a twelve-character sender and `"sms_flash": "true"` without complaint, and what reaches the phone in such cases
is not known. A client that passes such values on fails in production in ways no test sees. TellusTalk Emulator records
each such value as a warning on the message.

## Considered Options

- Send what the app passes, and leave the checking to TellusTalk.
- Normalise what can be normalised, such as a national number into `+46…` or a long sender cut to 11 characters.
- Refuse what the specification forbids, where the value is set.

## Decision Outcome

**A value the specification forbids is refused with an `ArgumentException` where it is set, in an `SmsMessage`
property's `init` or in the factory of an `SmsSender` or a `ReportAddress`, so a message that exists can be sent as
meant.** Recipients and numbers are `+` followed by digits, a sender name is at most 11 characters, a report address is
an http(s) URL, an e-mail address or a number, `MaxParts` is at least 1 and a billing id is letters and digits. Fields
whose wrong values the type system already excludes, such as the sender's source or the character set, are enums or
factories rather than strings.

Passing values on leaves the app's mistakes to be found on a phone. Normalising guesses at what the app meant: a
national number is Swedish only by assumption, and a cut sender name is a name nobody chose. The client checks no rule
that needs the text's encoding: whether a text is in the GSM alphabet under `Gsm`, or fits in `MaxParts`.

The tests send every kind of message to TellusTalk Emulator's image and require it to hold them without warnings, so the
client and the emulator, which read the specification separately, check each other.

## Consequences

- **Good:** a mistake surfaces in the app's own tests, at the line that made it, with the rule in its message.
- **Bad:** a value TellusTalk would accept as meant, but the specification forbids, cannot be sent; the tests need
  Docker.
- **Follow-up:** none.

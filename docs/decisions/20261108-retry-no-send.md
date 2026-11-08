---
status: accepted
date: 2026-11-08
deciders: [Peter Hedberg]
---

# ADR 20261108-retry-no-send: Retry no send

## Context and Problem Statement

A typed client registered through `IHttpClientFactory` usually comes with a resilience handler that retries failed and
timed-out requests. A send to TellusTalk is a `POST` with no idempotency key: a request that timed out, or whose
connection broke after it was written, may have been accepted, and its SMS sent.

## Considered Options

- Add a standard resilience handler with retries.
- Add no retries, and leave the choice to the app.

## Decision Outcome

**`AddTellusTalkClient` adds no handler that retries; it returns the client's `IHttpClientBuilder`, so an app can add
its own.** The README asks an app that adds one to leave `POST` out of its retries.

A retry after a timeout sends the SMS twice when the first attempt went through, and a recipient who gets a reminder or
a code twice is worse off than an app that logs one failed send.

## Consequences

- **Good:** the client never sends an SMS twice on its own.
- **Bad:** a send that fails on a passing network fault fails, and the app decides what to do.
- **Follow-up:** none.

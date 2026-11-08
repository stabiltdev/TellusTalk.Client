# Conventions

How code and tests are written here. A convention is a summary that cites its source; if this page and the code
disagree, the code is right and the page is a bug.

## Names

**Source:** code: `Stabilt.TellusTalk.Client.slnx`, `src/`, `tests/`

- The repository is named after the product, `TellusTalk.Client`; the organisation it lives in already says whose it
  is.
- Everything .NET inside it carries the company prefix, as .NET's namespace guidelines and NuGet's prefix reservation
  ask: the solution `Stabilt.TellusTalk.Client.slnx`, and each project, assembly, root namespace and package,
  `Stabilt.TellusTalk.Client[.<part>]`. A project's tests are `<project>.Tests`.
- `AddTellusTalkClient` is the exception for namespaces: it lives in `Microsoft.Extensions.DependencyInjection`, as the
  Microsoft.Extensions registrations do, so an app finds it without a `using`.
- A property of the message is named after TellusTalk's field without its `sms_` prefix, such as `MaxParts` for
  `sms_max_parts`, unless one type stands for several fields, as `Sender` does for `sms_originator_*`.

## The message

**Source:** [ADR 20261108-refuse-what-tellustalks-specification-forbids](decisions/20261108-refuse-what-tellustalks-specification-forbids.md);
code: `src/Stabilt.TellusTalk.Client/SmsMessage.cs`, `Sender.cs`, `ReportAddress.cs`

- A rule of the specification is checked where the value is set: in the property's `init`, or in the factory that
  builds a `Sender` or a `ReportAddress`. A message that exists is one that can be sent.
- A broken rule throws `ArgumentException` or `ArgumentOutOfRangeException`. From an `init`, the parameter is `value`,
  as in the base class library, and the message opens with the property's name: `MaxParts is at least 1.`
- `SendRequestBody` alone knows TellusTalk's field names and forms; a field the message leaves out is null there and is
  not sent.

## Tests

**Source:** code: `tests/Stabilt.TellusTalk.Client.Tests/`

- xUnit v3 on Microsoft Testing Platform, and FluentAssertions 8, used under its licence for non-commercial and
  open-source work. Run them with `dotnet test --solution Stabilt.TellusTalk.Client.slnx`, narrowed with
  `--filter-class`.
- A test of a request registers the client as an app does, with `Clients.Services`, and `RecordingHandler` in place of
  the network. It compares the body as sent, so the field order and the escaping are pinned too.
- A rule of the specification has a test that the client refuses it, and the emulator's tests show that what the client
  sends carries no warnings.
- A test name is the rule it pins, as a sentence: `A_scheduled_time_is_sent_with_its_offset`.
- Numbers come from PTS's range for fiction, `+46701740605` to `+46701740699`; names and texts are invented and name no
  real person, company or product. Each emulator test sends to numbers of its own.

## Code style

**Source:** code: `.editorconfig`, `Directory.Build.props`

- LINQ is a method chain, never query syntax, with one call per line once there are two; separate conditions are
  separate `Where` calls. An assertion chain stays on one line.
- A comment says what the code cannot: a constraint, a rejected alternative, a consequence out of sight. It never
  restates a name, and it describes the code as it is, not the change that made it.
- An argument guard such as `ArgumentNullException.ThrowIfNull` belongs on a public member of a public type only.
- An interface needs a purpose: something that uses the abstraction. A class without state is static and unregistered.
- Packages are versioned centrally in `Directory.Packages.props`.
- Analyzers run in every build and fail the Release build: build `-c Release` before pushing, and keep
  `dotnet format --verify-no-changes` clean. A suppression names its reason.

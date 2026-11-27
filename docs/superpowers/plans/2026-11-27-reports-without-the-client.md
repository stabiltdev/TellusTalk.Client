# Receiving Reports Without the Client's Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An app that receives delivery reports needs only `TellusTalk:Reports:Secret` and its route, and the same
settings configure one app that sends and receives as they configure two apps that split the work.

**Architecture:** `TellusTalkReportOptions` validates itself, with `Url` optional, and `TellusTalkOptions` adds only
that a sender's `Reports` needs `Url`. `AddTellusTalkReports<THandler>` takes the arguments `AddTellusTalkClient` takes
and binds the section's `Reports` into options of its own; the endpoint reads those, and no longer needs the client.
A new decision record supersedes ADR 20261125.

**Tech Stack:** .NET 10, Microsoft.Extensions.Options with DataAnnotations, ASP.NET Core minimal endpoints, xUnit v3 on
Microsoft Testing Platform, FluentAssertions 8, Microsoft.AspNetCore.TestHost, Testcontainers with TellusTalk Emulator.

**Spec:** `docs/superpowers/specs/2026-11-27-reports-without-the-client-design.md`

## Global Constraints

- Work in `E:\.worktrees\TellusTalk.Client\reports-without-the-client` on `feat/reports-without-the-client`, and run
  `dotnet` from there: `global.json` sets the test runner.
- `net10.0`; no package is added or raised.
- The configuration keys do not change: `TellusTalk:Reports:Url`, `:Secret`, `:PreviousSecrets`. Every validation
  message keeps its current wording, which the tests match.
- `AddTellusTalkClient`, `TellusTalkOptions`' public members and `MapTellusTalkReports`' signatures do not change.
- The repository's conventions hold (`docs/conventions.md`): one call per line in a chain, LINQ as a method chain,
  guards only on public members of public types, comments only for what the code cannot say, test names that state the
  rule, every public member with an XML summary, numbers from `+46701740605` to `+46701740699`.
- Everything written is English. No customer of TellusTalk is named anywhere.
- Each task ends green on `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`,
  `dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes` and
  `dotnet test --solution Stabilt.TellusTalk.Client.slnx` (Docker running), and is committed with a Conventional
  Commits message, lowercase after the colon, ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` and no
  session link.

## Review Focus

- A receiving app started for build-time OpenAPI generation, without its secret: `AddTellusTalkReports` must skip
  `ValidateOnStart` there as `AddTellusTalkClient` does, or the generation fails. The entry assembly cannot be faked in
  a test, so the reviewer checks the guard — Task 2.
- `AddTellusTalkReports<H>(builder.Configuration.GetSection("TellusTalk:Reports"))`, the `Reports` section passed by
  mistake: it binds `TellusTalk:Reports:Reports` and must fail at start naming `Reports:Secret`, not take reports
  without a secret — Task 2, `Passing_the_Reports_section_rather_than_its_parent_fails_when_the_app_starts`.
- A secret rotated in configuration reaches the receiving app's endpoint without a restart, now through
  `IOptionsMonitor<TellusTalkReportOptions>`: `A_secret_rotated_in_configuration_is_taken_without_a_restart` runs on the
  receiving app alone and must stay green — Task 2.
- An app that both sends and receives from one section starts with both validations, the sender's requiring `Url` and
  the receiver's not, and the secret the client puts in a message is the one the endpoint takes: the emulator's
  tests, whose app registers both, must stay green — Task 2.
- An app that registers only the client and maps the endpoint is still refused, naming `AddTellusTalkReports` —
  Task 2, `Mapping_the_endpoint_without_the_reports_registration_is_refused`.

## Files

| Path | Change |
|---|---|
| `src/Stabilt.TellusTalk.Client/TellusTalkReportOptions.cs` | Validates itself; `Url` optional there |
| `src/Stabilt.TellusTalk.Client/TellusTalkOptions.cs` | Requires `Reports:Url` and hands the rest to the report settings |
| `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsServiceCollectionExtensions.cs` | Three overloads binding the report settings |
| `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsEndpointRouteBuilderExtensions.cs` | Reads the report settings; needs no client |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportRegistrationTests.cs` | New: the registration's calls and rules |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReportApp.cs` | The receiving app alone |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportEndpointTests.cs` | Apps without the client |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs` | The new call |
| `README.md`, `AGENTS.md` | Delivery reports; the test project's row |
| `docs/decisions/20261127-receive-delivery-reports-with-the-report-settings-alone.md` | New record |
| `docs/decisions/20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200.md`, `docs/decisions/README.md` | Superseded |

---

### Task 1: The report settings validate themselves

A refactoring: no behaviour changes, so the existing tests are its proof.

**Files:**
- Modify: `src/Stabilt.TellusTalk.Client/TellusTalkReportOptions.cs`, `src/Stabilt.TellusTalk.Client/TellusTalkOptions.cs`
- Test: `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs` (unchanged)

**Interfaces:**
- Produces: `public sealed class TellusTalkReportOptions : IValidatableObject`. Its explicit
  `IValidatableObject.Validate` checks `Secret` (required; visible ASCII without spaces), `PreviousSecrets` (no empty
  secret; each visible ASCII) and `Url` only when it is set (an absolute http or https URL). Task 2 relies on
  `ValidateDataAnnotations()` running exactly these rules on `TellusTalkReportOptions`.

- [ ] **Step 1: Run the report settings' tests as the baseline**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class Stabilt.TellusTalk.Client.Tests.ReportSettingsTests`
Expected: PASS, every test.

- [ ] **Step 2: Move the report rules into `TellusTalkReportOptions`**

`TellusTalkReportOptions` implements `IValidatableObject`, with the rules and messages that `TellusTalkOptions.Validate`
holds today for `Secret`, `PreviousSecrets` and a set `Url`, and `IsVisibleAscii` moved with them (its comment too).
Write the rules once: an `internal IEnumerable<ValidationResult> Validate(bool urlRequired)` on
`TellusTalkReportOptions`, which the interface calls with `false`. `TellusTalkOptions.Validate` keeps its `BaseUrl`
check and, when `Reports` is set, yields `Reports.Validate(urlRequired: true)`; a missing `Url` gives today's message,
"Reports:Url is an absolute http or https URL, such as https://app.example/sms/reports, not ''." The class summary of
`TellusTalkReportOptions` gains a clause that a set `Url` is checked, and a missing one is the sender's to require.

- [ ] **Step 3: Run the whole suite**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: PASS, every test; `ReportSettingsTests` unchanged and green.

- [ ] **Step 4: Build like CI and check the format**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release` and
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`
Expected: no warnings, no errors, no format changes.

- [ ] **Step 5: Commit**

```bash
git add src/Stabilt.TellusTalk.Client/TellusTalkReportOptions.cs src/Stabilt.TellusTalk.Client/TellusTalkOptions.cs
git commit -m "refactor: let the report settings validate themselves" -m "The report endpoint can then validate the report settings without the client's, where Url is optional; the client still requires it." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: `AddTellusTalkReports` reads the report settings itself

**Files:**
- Modify: `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsServiceCollectionExtensions.cs`,
  `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsEndpointRouteBuilderExtensions.cs`
- Create: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportRegistrationTests.cs`
- Modify: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReportApp.cs`,
  `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportEndpointTests.cs`,
  `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs`

**Interfaces:**
- Consumes: `TellusTalkReportOptions : IValidatableObject` from Task 1.
- Produces, in `Microsoft.Extensions.DependencyInjection.TellusTalkReportsServiceCollectionExtensions`, each returning
  `IServiceCollection` and constrained `where THandler : class, ITellusTalkReportHandler`:
  - `AddTellusTalkReports<THandler>(this IServiceCollection services, IConfiguration configuration, string sectionName = "TellusTalk")`
  - `AddTellusTalkReports<THandler>(this IServiceCollection services, IConfigurationSection section)`, where `section`
    is the `TellusTalk` section itself and its `Reports` is bound
  - `AddTellusTalkReports<THandler>(this IServiceCollection services, Action<TellusTalkReportOptions> configureOptions)`
  - The overload without arguments is removed.
- Produces: the default-named `IOptionsMonitor<TellusTalkReportOptions>`, which the endpoint reads.

- [ ] **Step 1: Write the registration's tests**

Create `ReportRegistrationTests` with a private helper that builds an app on `TestServer` from a settings dictionary
and a registration, adds `ReceivedReports`, maps `MapTellusTalkReports("/sms/reports")` and starts it, as `ReportApp`
does. Each test posts `ReportBodies.Delivered` with `ReportApp.PostAsync`.

| Test | Settings | Registration | Assertion |
|---|---|---|---|
| `An_app_with_only_the_report_secret_takes_reports_at_its_route` | `TellusTalk:Reports:Secret` = `s3cret` | `(builder.Configuration)` | `200`; the handler got one report |
| `An_app_with_only_the_report_secret_turns_away_a_report_without_it` | as above | as above | `"Report wrong"` → `401`; none received |
| `A_section_of_the_apps_own_name_is_read_under_the_configuration` | `Sms:Reports:Secret` = `s3cret` | `(builder.Configuration, "Sms")` | `200` |
| `A_section_passed_alone_is_read_as_the_TellusTalk_section` | `Sms:Reports:Secret` = `s3cret` | `(builder.Configuration.GetSection("Sms"))` | `200` |
| `Code_alone_configures_the_reports` | none | `(reports => reports.Secret = "coded")` | `"Report coded"` → `200` |
| `An_app_that_also_sends_takes_reports_with_the_secret_of_its_one_section` | `UserName`, `Password`, `Reports:Url`, `Reports:Secret` under `TellusTalk` | `AddTellusTalkClient(builder.Configuration)` and `(builder.Configuration)` | starts; `200` |
| `Passing_the_Reports_section_rather_than_its_parent_fails_when_the_app_starts` | `TellusTalk:Reports:Secret` = `s3cret` | `(builder.Configuration.GetSection("TellusTalk:Reports"))` | starting throws `OptionsValidationException`, `*Reports:Secret*` |

And a theory, `Report_settings_that_break_a_rule_fail_when_the_app_starts(string key, string value)`, over a receiving
app with `TellusTalk:Reports:Secret` = `s3cret` and `key` set to `value`; starting it throws
`OptionsValidationException` with the message `*Reports:*`:

| `key` | `value` |
|---|---|
| `TellusTalk:Reports:Url` | `/sms/reports` |
| `TellusTalk:Reports:Url` | `ftp://app.invalid/sms/reports` |
| `TellusTalk:Reports:Secret` | ` ` |
| `TellusTalk:Reports:Secret` | `s3c ret` |
| `TellusTalk:Reports:PreviousSecrets:0` | `` (empty) |
| `TellusTalk:Reports:PreviousSecrets:0` | `0ld ` |

Then move the existing apps to the receiving side alone:
- `ReportApp`: no `AddTellusTalkClient` and no `Credentials`; `Settings` holds `Reports:Url`, `Reports:Secret` and
  `Reports:PreviousSecrets:0` as today; it registers `AddTellusTalkReports<THandler>(builder.Configuration)`.
- `ReportEndpointTests`: `Unstarted` and `A_body_the_server_refuses_is_answered_with_the_servers_status` register only
  `AddTellusTalkReports<RecordingReportHandler>(builder.Configuration)`;
  `Mapping_the_endpoint_without_a_route_and_without_an_absolute_report_url_is_refused` starts from empty settings;
  `Mapping_the_endpoint_with_a_handler_registered_by_hand_is_refused` registers the handler alone, without the client;
  `The_reports_registration_without_report_settings_fails_when_the_app_starts` adds no settings after clearing them.
  `Mapping_the_endpoint_without_the_reports_registration_is_refused` keeps the client alone.
- `EmulatorReportApp`: `AddTellusTalkReports<RecordingReportHandler>(builder.Configuration)` beside the client.

- [ ] **Step 2: Build to see the tests fail**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx`
Expected: FAIL with CS1501, no overload of `AddTellusTalkReports` takes 1 or 2 arguments.

- [ ] **Step 3: Register the report settings in `AddTellusTalkReports`**

The three overloads as in Interfaces, mirroring `AddTellusTalkClient`: the configuration overload takes
`configuration.GetSection(sectionName)`; the section overload binds `section.GetSection("Reports")`. A private
`Register<THandler>(OptionsBuilder<TellusTalkReportOptions>)` calls `ValidateDataAnnotations()`, calls
`ValidateOnStart()` unless the entry assembly is `GetDocument.Insider` (a private copy of the client's check, with its
comment, since the packages share no internals), registers the handler scoped and the marker with `TryAddSingleton`, and
returns the services. The validation of `TellusTalkOptions` that this method added goes. The summaries say that the
app's handler and the report settings are registered, that only `Reports:Secret` is required, and that a wrong setting
fails when the app starts.

- [ ] **Step 4: Read the report settings at the endpoint**

In `TellusTalkReportsEndpointRouteBuilderExtensions`:
- `EnsureRegistered` checks the marker alone: "MapTellusTalkReports needs AddTellusTalkReports in the app's services."
- `ReportUrl` builds an `OptionsFactory<TellusTalkReportOptions>` and returns `Create(Options.DefaultName).Url`.
- `RequireReportSecret` reads `IOptionsMonitor<TellusTalkReportOptions>.CurrentValue`; the comment about `Reports`
  being set goes, since the settings always exist.
- The `<exception>` docs of both overloads name `AddTellusTalkReports` alone.

- [ ] **Step 5: Run the AspNetCore tests**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class Stabilt.TellusTalk.Client.AspNetCore.Tests.ReportRegistrationTests`
then the whole suite: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: PASS, every test, the emulator's included.

- [ ] **Step 6: Build like CI and check the format**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release` and
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`
Expected: no warnings, no errors, no format changes.

- [ ] **Step 7: Commit**

```bash
git add src/Stabilt.TellusTalk.Client.AspNetCore tests/Stabilt.TellusTalk.Client.AspNetCore.Tests
git commit -m "feat!: receive reports with the report settings alone" -m "An app that receives reports while another sends held K8's credentials only because the endpoint read the client's settings. AddTellusTalkReports binds the report settings itself, from the section AddTellusTalkClient reads, so one app that does both is configured as the two apps together." -m "BREAKING CHANGE: AddTellusTalkReports takes the app's configuration, a section or code, as AddTellusTalkClient does; the overload without arguments is gone." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: The README and the decision record

**Files:**
- Modify: `README.md`, `AGENTS.md`
- Create: `docs/decisions/20261127-receive-delivery-reports-with-the-report-settings-alone.md`
- Modify: `docs/decisions/20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200.md` (status only),
  `docs/decisions/README.md`

**Interfaces:**
- Consumes: the overloads and rules of Task 2, as the README describes them.

- [ ] **Step 1: Update the README's delivery reports**

- The example registers `AddTellusTalkReports<SmsReportHandler>(builder.Configuration)`.
- A table of `AddTellusTalkReports`' calls, as the client's has: `(builder.Configuration)` reads `TellusTalk:Reports`;
  `(builder.Configuration, "Sms")` reads `Sms:Reports`; `(builder.Configuration.GetSection("Sms"))` reads that
  section's `Reports`; `(reports => …)` reads nothing, and the code sets the report settings. One line that an app doing
  both passes the same section to both calls, and from code gives the secret to both.
- The rules: `Reports` is optional for the client, and when there `Url` and `Secret` are required;
  `AddTellusTalkReports` reads the same `Reports` and requires only `Secret`, and a `Url` there is checked when set and
  needed only by `MapTellusTalkReports()` without a route.
- An app that receives while another sends: its settings, `Reports:Secret` alone, and its code,
  `AddTellusTalkReports<…>(builder.Configuration)` and `MapTellusTalkReports("/sms/reports")`.
- Rotation: where reports are taken by several instances, or by another app than the one that sends, the new secret is
  first listed in `PreviousSecrets` wherever reports are taken, and only then made `Secret` where messages are sent.

In `AGENTS.md`, the row of `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests` reads "The registration, and the
endpoint's answers on `TestServer`, to the secret and the body".

- [ ] **Step 2: Commit the README**

```bash
git add README.md AGENTS.md
git commit -m "docs: describe receiving reports with the report settings alone" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: Write the decision record**

Following `docs/decisions/README.md`'s rules for superseding, with the identity
`20261127-receive-delivery-reports-with-the-report-settings-alone` and the title "Receive delivery reports at the
Reports URL every message carries, checked by a secret in Authorization, answered 200, and configured by the report
settings alone". It restates ADR 20261125's whole decision, with these changes:
- Context opens with what changed: an app that received reports while another sent them had to hold K8's user name and
  password and an absolute `Reports:Url`, since the endpoint read the client's settings.
- Considered options add: the report settings registered by `AddTellusTalkReports` from the section the client reads;
  one settings class whose credentials are required only where the client is registered.
- The outcome: `AddTellusTalkReports<THandler>` takes the arguments `AddTellusTalkClient` takes and binds the section's
  `Reports`, requiring only `Secret`, with `Url` checked when set; the client still requires `Url` and `Secret` when
  `Reports` is set. The same keys configure one app that does both and two apps that split it. The rejected option's
  reason: its rules would depend on which registration ran, which nothing in the class shows, and the receiving app
  would need its section bound all the same.
- Consequences: the follow-up is answered and goes. Bad: an app that does both from code gives the secret twice; with
  two apps, a rotation reaches the receiving app first.

In ADR 20261125, change only `status` to `superseded by [ADR 20261127-…](20261127-….md)`. In the index, change its row's
status and add the new row.

- [ ] **Step 4: Check the suite and the format**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx` and
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`
Expected: PASS; no format changes.

- [ ] **Step 5: Commit the record**

```bash
git add docs/decisions
git commit -m "docs: record that the report endpoint reads the report settings alone" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

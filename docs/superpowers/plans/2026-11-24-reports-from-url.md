# Report Address and Route from `Reports:Url` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Reports:Url` is stated once: the client puts it, carrying the secret, in every message's `report_to`, and
`app.MapTellusTalkReports()` maps the endpoint at its path.

**Architecture:** `TellusTalkClient.SendAsync` builds the report address from the current `Reports` settings and hands
it to `SendRequestBody.From`, which opens `report_to` with it; `TellusTalkClient.ReportAddress` goes. The ASP.NET Core
package gains a parameterless `MapTellusTalkReports()` that reads `Reports:Url` without validating the other settings
and maps the pattern overload at its path. A new decision record supersedes the one from 2026-11-23.

**Tech Stack:** .NET 10, Microsoft.Extensions.Options, ASP.NET Core minimal endpoints, xUnit v3 on Microsoft Testing
Platform, FluentAssertions 8, Microsoft.AspNetCore.TestHost, Testcontainers with TellusTalk Emulator.

**Spec:** `docs/superpowers/specs/2026-11-24-reports-from-url-design.md`

## Global Constraints

- Work in `E:\.worktrees\TellusTalk.Client\reports-from-url` on `feat/reports-from-url`, and run `dotnet` from there:
  `global.json` sets the test runner.
- `net10.0`; no package is added or raised.
- The report address is `ReportAddress.Url(Reports.Url, { ["Authorization"] = "Report <secret>" })`, built with
  `ReportAuthorization.HeaderValue`, read from `IOptionsMonitor<TellusTalkOptions>.CurrentValue` at each send, and sent
  first in `report_to`, before the message's own `ReportTo`.
- Without `Reports`, `report_to` is the message's `ReportTo` alone, and is left out when that is empty.
- `MapTellusTalkReports(pattern)` keeps its signature and behaviour.
- The repository's conventions hold (`docs/conventions.md`): one call per line in a chain, LINQ as a method chain,
  guards only on public members of public types, comments only for what the code cannot say, test names that state the
  rule, every public member with an XML summary, numbers from `+46701740605` to `+46701740699`.
- Everything written is English.
- Each task ends green on `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`,
  `dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes` and
  `dotnet test --solution Stabilt.TellusTalk.Client.slnx` (Docker running), and is committed with a Conventional
  Commits message, lowercase after the colon, ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` and no
  session link.

## Review Focus

- A `Reports:Url` with a query, such as `?source=k8`: the address keeps the query, and the endpoint is mapped at the
  path without it — Tasks 1 and 2.
- An app started for build-time OpenAPI generation, with `Reports:Url` but without credentials or a secret:
  `MapTellusTalkReports()` maps without throwing — Task 2.
- `MapTellusTalkReports()` in a route group: refused, rather than mapped at the group's prefix plus the whole path —
  Task 2.
- `Reports` missing, `Reports:Url` missing, or a relative `Reports:Url` when mapping without a route: refused with a
  message naming `Reports:Url`, not `Uri`'s "This operation is not supported for a relative URI" — Task 2.
- A message without report settings and without `ReportTo` sends no `report_to`: already pinned by
  `SendingTests.A_message_of_recipients_and_text_sends_those_fields_alone_and_unescaped`, which must stay green.

## Files

| Path | Change |
|---|---|
| `src/Stabilt.TellusTalk.Client/TellusTalkClient.cs` | `ReportAddress` removed; `SendAsync` passes the report address on |
| `src/Stabilt.TellusTalk.Client/SendRequestBody.cs` | `From` takes the report address and opens `report_to` with it |
| `src/Stabilt.TellusTalk.Client/SmsMessage.cs`, `TellusTalkOptions.cs`, `TellusTalkReportOptions.cs` | XML summaries |
| `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsEndpointRouteBuilderExtensions.cs` | `MapTellusTalkReports()` |
| `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs` | The address in every message |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportEndpointTests.cs` | The route from `Reports:Url` |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReportApp.cs` | Maps without a route by default |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/*.cs` | Sends without `ReportTo`; maps without a route |
| `README.md` | Delivery reports, the sending example, the message table |
| `docs/decisions/20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md` | New record |
| `docs/decisions/20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md`, `docs/decisions/README.md` | Superseded |

---

### Task 1: Every message carries the report address

**Files:**
- Modify: `src/Stabilt.TellusTalk.Client/TellusTalkClient.cs`, `src/Stabilt.TellusTalk.Client/SendRequestBody.cs`
- Modify (summaries): `src/Stabilt.TellusTalk.Client/SmsMessage.cs:53`, `TellusTalkOptions.cs:26`,
  `TellusTalkReportOptions.cs:6`
- Test: `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs`
- Modify: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/ReportsFromTheEmulatorTests.cs`

**Interfaces:**
- Produces: `internal static SendRequestBody SendRequestBody.From(SmsMessage message, ReportAddress? reportAddress)`.
  `TellusTalkClient.ReportAddress` no longer exists; nothing later uses it.

- [ ] **Step 1: Rewrite the report-address tests in `ReportSettingsTests.cs`**

Replace `The_report_address_carries_the_secret_in_authorization` and delete
`The_report_address_without_report_settings_is_refused`. `Message` (no `ReportTo`) and `Field` stay as they are.

```csharp
[Fact]
public async Task Every_message_carries_the_report_address_with_the_secret_in_authorization()
// Clients.Services(handler, Clients.WithReports()); SendAsync(Message)
// Field(handler, "report_to") == """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports"]"""

[Fact]
public async Task The_report_address_comes_before_the_messages_own_report_addresses()
// WithReports(); SendAsync(Message with { ReportTo = [ReportAddress.Email("reports@app.invalid")] })
// report_to == """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports","email:reports@app.invalid"]"""

[Fact]
public async Task The_report_address_keeps_the_query_of_the_report_url()
// WithReports(url: "https://app.invalid/sms/reports?source=k8"); SendAsync(Message)
// report_to == """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports?source=k8"]"""

[Fact]
public async Task Without_report_settings_only_the_messages_own_report_addresses_are_sent()
// Clients.Services(handler); SendAsync(Message with { ReportTo = [ReportAddress.Email("reports@app.invalid")] })
// report_to == """["email:reports@app.invalid"]"""
```

In `The_report_address_follows_a_secret_rotated_in_configuration`, both sends become `client.SendAsync(Message, …)`;
the assertion stays.

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class "Stabilt.TellusTalk.Client.Tests.ReportSettingsTests"`
Expected: the three tests that send a message with `Reports` set fail on `report_to` missing (`KeyNotFoundException`);
`Without_report_settings_…` passes already.

- [ ] **Step 3: Add the report address in `SendRequestBody.From` and `TellusTalkClient.SendAsync`**

- `SendRequestBody.From(SmsMessage message, ReportAddress? reportAddress)`: `report_to` is
  `[reportAddress, .. message.ReportTo]` when `reportAddress` is not null, otherwise `message.ReportTo`, through the
  existing `Addresses` (empty → null).
- `TellusTalkClient`: remove `ReportAddress`. `SendAsync` reads `options.CurrentValue.Reports` and, when it is not null,
  builds the address as the removed property did (`ReportAddress.Url(reports.Url!, new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = ReportAuthorization.HeaderValue(reports.Secret) })`),
  in a private static method `ReportAddressOf(TellusTalkReportOptions? reports)` returning `ReportAddress?`.
- Summaries, saying what the name does not:
  - `SendAsync`: when the `Reports` settings are set, the message's `report_to` opens with their report address.
  - `SmsMessage.ReportTo`: where TellusTalk sends the message's delivery report beyond the report address the client adds
    when the `Reports` settings are set, such as an email address.
  - `TellusTalkOptions.Reports`: the client puts their address in every message, and the endpoint takes their reports;
    `null` for an app that takes none.
  - `TellusTalkReportOptions.Url`: as the world reaches the app; every message carries it, and `MapTellusTalkReports()`
    maps the endpoint at its path.

- [ ] **Step 4: Send the emulator's messages without `ReportTo`**

In `ReportsFromTheEmulatorTests.cs`, remove `ReportTo = [app.Client.ReportAddress]` from both messages, and make the
class summary say that a message is sent by a client with report settings, which give it the report address.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`, then
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`, then
`dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: build without warnings, format clean, every test passes, the emulator's two report tests among them.

- [ ] **Step 6: Commit**

```bash
git add src/Stabilt.TellusTalk.Client tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/ReportsFromTheEmulatorTests.cs
git commit -m "feat!: put the report address in every message when reports are set" -m "<why: a message without ReportTo lost its reports without a trace, and the address is already in the settings>" -m "BREAKING CHANGE: TellusTalkClient.ReportAddress is removed. The client opens every message's report_to with the address of the Reports settings; ReportTo holds only addresses beyond it." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `MapTellusTalkReports()` maps the endpoint at the path of `Reports:Url`

**Files:**
- Modify: `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsEndpointRouteBuilderExtensions.cs`
- Test: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportEndpointTests.cs`
- Modify: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReportApp.cs:43`,
  `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs:33,48`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `public static IEndpointConventionBuilder MapTellusTalkReports(this IEndpointRouteBuilder endpoints)`.

- [ ] **Step 1: Write the failing tests in `ReportEndpointTests.cs`**

The three `StartAsync` tests set `TellusTalk:Reports:Url` in `configure`, such as with an `AddInMemoryCollection` added
after `ReportApp.Settings`, and leave `map` at its default, which Step 3 makes `MapTellusTalkReports()`.

```csharp
[Fact]
public async Task The_endpoint_mapped_without_a_route_takes_reports_at_the_path_of_the_report_url()
// Reports:Url "https://app.invalid/hooks/tellustalk"; PostAsync(app, ReportBodies.Delivered, path: "/hooks/tellustalk")
// StatusCode == NoContent

[Fact]
public async Task The_query_of_the_report_url_is_not_part_of_the_route()
// Reports:Url "https://app.invalid/sms/reports?source=k8"; PostAsync(app, ReportBodies.Delivered)
// StatusCode == NoContent

[Fact]
public async Task A_route_given_to_the_endpoint_is_taken_instead_of_the_path_of_the_report_url()
// map: web => web.MapTellusTalkReports("/internal/reports"); PostAsync(…, path: "/internal/reports")
// StatusCode == NoContent

[Fact]
public Task Mapping_the_endpoint_without_a_route_in_a_route_group_is_refused()
// map: web => web.MapGroup("/integrations").MapTellusTalkReports()
// StartAsync should ThrowAsync<InvalidOperationException>().WithMessage("*route group*")

[Theory]
[InlineData(null, null)]
[InlineData(null, "s3cret")]
[InlineData("/sms/reports", "s3cret")]
public async Task Mapping_the_endpoint_without_a_route_and_without_an_absolute_report_url_is_refused(string? url, string? secret)
// a slim builder on TestServer with ReportApp.Credentials, plus TellusTalk:Reports:Url and :Secret when not null;
// AddTellusTalkClient(builder.Configuration) and AddTellusTalkReports<RecordingReportHandler>(); Build()
// (() => app.MapTellusTalkReports()) should Throw<InvalidOperationException>().WithMessage("*Reports:Url*")

[Fact]
public async Task Mapping_the_endpoint_without_a_route_reads_the_report_url_without_validating_the_other_settings()
// as above, but the configuration holds TellusTalk:Reports:Url "https://app.invalid/sms/reports" alone:
// no credentials and no secret, as in build-time OpenAPI generation. The app is not started.
// (() => app.MapTellusTalkReports()) should NotThrow()
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx`
Expected: CS7036 (no argument given for the required parameter `pattern`) in `ReportEndpointTests.cs`.

- [ ] **Step 3: Implement `MapTellusTalkReports(this IEndpointRouteBuilder endpoints)`**

- Both overloads first run the current check that `AddTellusTalkReports` and `AddTellusTalkClient` are registered, moved
  into a private method, so a missing registration is named before a missing setting.
- `endpoints is RouteGroupBuilder` throws `InvalidOperationException("MapTellusTalkReports() maps the whole path of Reports:Url, which a route group's prefix would go in front of; in a route group, give it the route.")`.
- The URL is read without the options' validation: `new OptionsFactory<TellusTalkOptions>(services.GetServices<IConfigureOptions<TellusTalkOptions>>(), services.GetServices<IPostConfigureOptions<TellusTalkOptions>>()).Create(Options.DefaultName).Reports?.Url`,
  with a comment that build-time OpenAPI generation runs the app without its secrets. The app's start still validates
  everything.
- A URL that is null or not absolute throws `InvalidOperationException("MapTellusTalkReports() maps the path of Reports:Url, an absolute http or https URL; set it, or give the route.")`.
- The route is `Uri.UnescapeDataString(url.AbsolutePath)`, since routing matches the request's decoded path, and the
  method returns `endpoints.MapTellusTalkReports(route)`.
- The summary says the route is the path of `Reports:Url`, read when the endpoint is mapped, so a changed path needs a
  restart; that a proxy that rewrites paths, `UsePathBase` or a route group needs the pattern overload; and its
  `<exception>` names the route group and the missing URL besides the registrations. The pattern overload's `pattern`
  documentation says it is for a route that differs from the path of `Reports:Url`.
- `ReportApp.StartAsync`'s default becomes `map ??= web => web.MapTellusTalkReports();`.
- `EmulatorReportApp` maps with `_app.MapTellusTalkReports();`; its comment says the stand-in report URL carries the path
  the endpoint is mapped at, which the URL set once the port is known keeps.

- [ ] **Step 4: Run the whole suite**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`, then
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`, then
`dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: every test passes, the endpoint's existing tests now through `MapTellusTalkReports()`, and the emulator's
reports reaching the handler at the path of the stand-in URL.

- [ ] **Step 5: Commit**

```bash
git add src/Stabilt.TellusTalk.Client.AspNetCore tests/Stabilt.TellusTalk.Client.AspNetCore.Tests
git commit -m "feat: map the report endpoint at the path of Reports:Url" -m "<why: the app states the address once; a route group or a rewriting proxy gives the route, since the path there is not the route>" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The README and the decision record

**Files:**
- Modify: `README.md`
- Create: `docs/decisions/20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md`
- Modify: `docs/decisions/20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md` (status only),
  `docs/decisions/README.md`

- [ ] **Step 1: Update the README**

- The sending example (`README.md:36`) loses its `ReportTo` line.
- The message table's `ReportTo` row: addresses beyond the report address the client adds when `Reports` is set, in the
  forms it lists today.
- Delivery reports (`README.md:91-184`): the mapping becomes `app.MapTellusTalkReports();`, and the sending example
  loses `ReportTo`. The paragraph at `README.md:141-147` says instead: when `Reports` is set, the client opens every
  message's `report_to` with `Reports:Url` carrying the secret as `Authorization: Report <secret>`, and `ReportTo` adds
  addresses beyond it; `MapTellusTalkReports()` maps the endpoint at the path of `Reports:Url`, read when the app maps
  it, so a changed path needs a restart; where the app receives on another route than TellusTalk calls, behind a proxy
  that rewrites paths such as Azure Front Door, under `UsePathBase` or in a route group, the app gives the route,
  `MapTellusTalkReports("/internal/route")`. What the paragraph says about `Reports` being optional, the required
  settings, https and the secret stays.
- `README.md:183-184`: an app without ASP.NET Core sets `Reports` too, so the client puts the address in its messages.

- [ ] **Step 2: Write the decision record**

Copy `docs/decisions/template.md` to
`docs/decisions/20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md`, front matter
`status: accepted`, `date: 2026-11-24`, `deciders: [Peter Hedberg]`,
`supersedes: [ADR 20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret](20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md)`.
Title: `Receive delivery reports at the Reports URL every message carries, checked by a secret in Authorization`.
One page.

- **Context** opens with what changed: the app stated the address twice, in `Reports:Url` and in
  `MapTellusTalkReports(pattern)`, and put `TellusTalkClient.ReportAddress` in each message itself, so a message
  without it lost its reports without a trace. Then the superseded record's context: K8 signs nothing, and the endpoint
  is reachable from the internet.
- **Considered options**, one line each: the app adds the address to each message; the client adds it to every message
  when `Reports` is set; the route given apart from `Reports:Url`; the route taken from the path of `Reports:Url`, with
  a route given where they differ; only the app's public origin in the settings, the address built from the mapped
  endpoint's route; the address built from the current request; and, from the superseded record, reading only, and the
  secret in a header of the package's own or as a `Bearer` token.
- **Decision outcome**, in bold first: every message carries the report address of `Reports:Url`, and the endpoint
  `Stabilt.TellusTalk.Client.AspNetCore` maps takes a report only when its `Authorization` is `Report <secret>`. Then the
  types: `TellusTalkOptions.Reports` (`Url`, `Secret`, `PreviousSecrets`); `SendAsync` opens `report_to` with the
  address, before `SmsMessage.ReportTo`; `AddTellusTalkReports<THandler>()`; `MapTellusTalkReports()` at the path of
  `Reports:Url`, `MapTellusTalkReports(pattern)` where the route differs; `DeliveryReport` and `ReportAuthorization`
  in the main package. Then why: the address is stated once and a message cannot leave it out; the public address
  cannot come from the app itself, since a scheduled send has no request and `Host` is the caller's to set, so a forged
  `Host` would send the secret elsewhere; the public origin with `LinkGenerator` was rejected because the address would
  exist only in an app that maps the endpoint. The superseded record's reasons that still hold are kept as they stand:
  the check written once, the main package free of ASP.NET Core, the `Report` scheme, the required secret,
  `PreviousSecrets` read through `IOptionsMonitor`, and the status codes.
- **Consequences**: the superseded record's, where they still hold, with these added. Bad: in a route group or behind
  a rewriting proxy the route is given besides the URL; a changed path needs a restart; a message cannot yet leave the
  address out. Follow-up: an app that only receives reports needs K8's credentials, since the endpoint reads the
  client's settings.

- [ ] **Step 3: Supersede the old record**

In `20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md`, change only `status` to
`superseded by [ADR 20261124-receive-delivery-reports-at-the-reports-url-every-message-carries](20261124-receive-delivery-reports-at-the-reports-url-every-message-carries.md)`.
In `docs/decisions/README.md`, set that row's status to the same, and add the new record's row with its title and
`accepted`.

- [ ] **Step 4: Check**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`, and `grep -n "ReportAddress\|MapTellusTalkReports(\"" README.md`
Expected: the build passes; the grep shows `ReportAddress` only as `ReportAddress.Url`, `.Email` or `.Sms`, never
`tellusTalk.ReportAddress`, and `MapTellusTalkReports("…")` only where the README gives a route on purpose. The decision
records name the removed members on purpose and are not searched.

- [ ] **Step 5: Commit, in two commits**

```bash
git add README.md
git commit -m "docs: show reports set up by Reports:Url alone" -m "<why>" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git add docs/decisions
git commit -m "docs: record that every message carries the report address of Reports:Url" -m "<why>" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

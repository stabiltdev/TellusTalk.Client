# Delivery Reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An ASP.NET Core app receives TellusTalk's delivery reports at an endpoint the package maps, checked by a secret
from the client's settings, and handles each one in a handler class of its own.

**Architecture:** The main package gains the report settings (`TellusTalkOptions.Reports`), the check of an
`Authorization` value (`ReportAuthorization`), the report address the client hands out (`TellusTalkClient.ReportAddress`)
and the report itself (`DeliveryReport`). A new package, `Stabilt.TellusTalk.Client.AspNetCore`, registers the app's
`ITellusTalkReportHandler` and maps the endpoint that checks, reads and hands on each report.

**Tech Stack:** .NET 10, System.Text.Json, Microsoft.Extensions.Options, ASP.NET Core minimal endpoints, xUnit v3 on
Microsoft Testing Platform, FluentAssertions 8, Microsoft.AspNetCore.TestHost, Testcontainers with TellusTalk Emulator
0.7.0.

**Spec:** `docs/superpowers/specs/2026-11-23-delivery-reports-design.md`

## Global Constraints

- Work in `E:\.worktrees\TellusTalk.Client\initial` on `feat/initial`, and run `dotnet` from there: `global.json` sets
  the test runner.
- `net10.0`. The main package keeps its 10.0.0 Microsoft.Extensions packages; the new package adds
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and no ASP.NET Core package.
- The secret travels as `Authorization: Report <secret>`; the scheme `Report` is compared case-insensitively, the secret
  exactly.
- The endpoint answers `204` for a report taken, `401` for a missing or wrong `Authorization`, `400` for a body that is
  not a report; a handler's exception is left to the app's error handling (`500`).
- The secret is required whenever the endpoint is used; nothing turns the check off.
- Nothing in the packages logs a report's body, a number or a secret.
- No customer names in code, tests or documents; numbers from `+46701740605` to `+46701740699`; the emulator tests here
  use `+46701740620` and `+46701740621`.
- The repository's conventions hold (`docs/conventions.md`): one call per line in a chain, LINQ as a method chain,
  guards only on public members of public types, comments only for what the code cannot say, test names that state the
  rule.
- Each task ends green on `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`,
  `dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes` and
  `dotnet test --solution Stabilt.TellusTalk.Client.slnx` (Docker running), and is committed with a Conventional
  Commits message ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A report whose optional fields have another type or format (`report_time` not ISO 8601, `log_status_code` as a
  string, `quantity` as text) still reaches the handler, with those properties `null` — Task 2.
- A body that is empty, JSON `null`, an array or not JSON is answered `400` and never reaches the handler or turns into
  a `500` — Task 3.
- `Authorization` in a lower-case scheme is taken; `Bearer`, the scheme alone, the scheme with a trailing space, and a
  secret of another length are answered `401` without an exception — Tasks 1 and 3.
- An app whose fallback authorization policy requires an authenticated user still takes reports — Task 3.
- A secret rotated in configuration while the app runs is taken at once, and the old one only while it is listed in
  `PreviousSecrets` — Task 3.

## Files

| Path | Responsibility |
|---|---|
| `src/Stabilt.TellusTalk.Client/TellusTalkReportOptions.cs` | The report settings: URL, secret, previous secrets |
| `src/Stabilt.TellusTalk.Client/TellusTalkOptions.cs` | Gains `Reports` and its validation |
| `src/Stabilt.TellusTalk.Client/ReportAuthorization.cs` | The `Report` scheme: the header value, and the check of a received one |
| `src/Stabilt.TellusTalk.Client/TellusTalkClient.cs` | Gains `ReportAddress` |
| `src/Stabilt.TellusTalk.Client/DeliveryStatus.cs`, `DeliveryReport.cs`, `DeliveryReportJsonConverter.cs` | A report as an app reads it, and how it is read from and written to K8's JSON |
| `src/Stabilt.TellusTalk.Client.AspNetCore/` | The new package: `ITellusTalkReportHandler`, `AddTellusTalkReports`, `MapTellusTalkReports` |
| `tests/Shared/ReportBodies.cs` | K8's report bodies, linked into both test projects |
| `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs`, `ReportAuthorizationTests.cs`, `DeliveryReportTests.cs` | The main package's new parts |
| `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/` | The endpoint against `TestServer`, and the whole path against the emulator |

---

### Task 1: Report settings, the `Report` scheme, and the client's report address

**Files:**
- Create: `src/Stabilt.TellusTalk.Client/TellusTalkReportOptions.cs`
- Create: `src/Stabilt.TellusTalk.Client/ReportAuthorization.cs`
- Modify: `src/Stabilt.TellusTalk.Client/TellusTalkOptions.cs` (property and `Validate`)
- Modify: `src/Stabilt.TellusTalk.Client/TellusTalkClient.cs` (constructor and `ReportAddress`)
- Modify: `tests/Stabilt.TellusTalk.Client.Tests/Support/Clients.cs` (add `WithReports`)
- Test: `tests/Stabilt.TellusTalk.Client.Tests/ReportSettingsTests.cs`, `tests/Stabilt.TellusTalk.Client.Tests/ReportAuthorizationTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed class TellusTalkReportOptions
{
    public Uri? Url { get; set; }
    public string Secret { get; set; } = string.Empty;
    public IList<string> PreviousSecrets { get; } = [];
}

// TellusTalkOptions
public TellusTalkReportOptions? Reports { get; set; }

public static class ReportAuthorization
{
    public const string Scheme = "Report";
    internal static string HeaderValue(string secret);          // "Report " + secret
    public static bool IsValid(string? authorization, TellusTalkReportOptions reports);
}

// TellusTalkClient
public TellusTalkClient(HttpClient httpClient, IOptionsMonitor<TellusTalkOptions> options);
public ReportAddress ReportAddress { get; }
```

- [ ] **Step 1: Add `Clients.WithReports` and write the failing settings tests**

`Clients.WithReports(string? url = "https://app.invalid/sms/reports", string? secret = "s3cret")` returns
`Credentials` plus `TellusTalk:Reports:Url` and `TellusTalk:Reports:Secret`, leaving out a key whose value is `null`.

```csharp
public sealed class ReportSettingsTests
{
    [Fact]
    public async Task The_report_address_carries_the_secret_in_authorization()
    {
        // Send Minimal with ReportTo = [client.ReportAddress] through Clients.Services(handler, Clients.WithReports()).
        Field(handler, "report_to").Should().Be(
            """["https:{\"headers\":{\"Authorization\":\"Report s3cret\"}}://app.invalid/sms/reports"]""");
    }

    [Fact]
    public async Task The_report_address_follows_a_secret_rotated_in_configuration()
    {
        // As RegistrationTests.A_password_changed_in_configuration_reaches_the_next_client:
        // configuration["TellusTalk:Reports:Secret"] = "rotated"; configuration.Reload(); then send again.
        Field(handler, "report_to").Should().Contain("Report rotated");
    }

    [Fact]
    public async Task The_report_address_without_report_settings_is_refused()
    {
        var address = () => client.ReportAddress;   // client registered with Clients.Credentials only
        address.Should().Throw<InvalidOperationException>().WithMessage("*Reports*");
    }

    [Theory]
    [InlineData(null, "s3cret")]
    [InlineData("/sms/reports", "s3cret")]
    [InlineData("ftp://app.invalid/sms/reports", "s3cret")]
    [InlineData("https://app.invalid/sms/reports", null)]
    [InlineData("https://app.invalid/sms/reports", " ")]
    public async Task Report_settings_without_an_http_url_or_a_secret_are_refused(string? url, string? secret)
    {
        var create = services.GetRequiredService<TellusTalkClient>;   // services from Clients.WithReports(url, secret)
        create.Should().Throw<OptionsValidationException>().WithMessage("*Reports*");
    }

    [Fact]
    public async Task An_empty_previous_secret_is_refused()
    {
        // Clients.WithReports() plus ["TellusTalk:Reports:PreviousSecrets:0"] = ""
        create.Should().Throw<OptionsValidationException>().WithMessage("*PreviousSecrets*");
    }
}
```

`Field` is the same helper as `SendingTests.Field`; copy it rather than share it across test classes.

- [ ] **Step 2: Write the failing scheme tests**

```csharp
public sealed class ReportAuthorizationTests
{
    private static readonly TellusTalkReportOptions Reports = new() { Url = new("https://app.invalid/sms/reports"), Secret = "s3cret", PreviousSecrets = { "0ld" } };

    [Theory]
    [InlineData("Report s3cret")]
    [InlineData("report s3cret")]
    [InlineData("REPORT s3cret")]
    [InlineData("Report 0ld")]
    public void A_secret_in_the_report_scheme_is_valid(string authorization) =>
        ReportAuthorization.IsValid(authorization, Reports).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("s3cret")]
    [InlineData("Report")]
    [InlineData("Report ")]
    [InlineData("Bearer s3cret")]
    [InlineData("Report s3cre")]
    [InlineData("Report s3cret2")]
    [InlineData("Report  s3cret")]
    public void Anything_else_is_not_valid(string? authorization) =>
        ReportAuthorization.IsValid(authorization, Reports).Should().BeFalse();
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class "*ReportSettingsTests" --filter-class "*ReportAuthorizationTests"`
Expected: the build fails; `TellusTalkReportOptions`, `ReportAuthorization` and `ReportAddress` do not exist.

- [ ] **Step 4: Implement the settings, the scheme and the address**

- `TellusTalkOptions.Validate` adds, when `Reports` is not null, one `ValidationResult` per broken rule with member
  name `nameof(Reports)`, worded as the `BaseUrl` rule is: `Reports:Url is an absolute http or https URL, such as
  https://app.example/sms/reports, not '…'.`, `Reports:Secret is required: TellusTalk's reports carry it, and the
  endpoint turns away a report without it.`, `Reports:PreviousSecrets holds an empty secret.`
- `ReportAuthorization.IsValid` splits at the first space, compares the scheme with
  `StringComparison.OrdinalIgnoreCase`, and compares the rest with `Secret` and each of `PreviousSecrets` by
  `CryptographicOperations.FixedTimeEquals` over `SHA256.HashData` of their UTF-8 bytes, so neither the length nor the
  content shows in the time it takes; it checks every candidate rather than stopping at the first match.
- `TellusTalkClient.ReportAddress` reads `options.CurrentValue.Reports` each time and returns
  `ReportAddress.Url(reports.Url, { ["Authorization"] = ReportAuthorization.HeaderValue(reports.Secret) })`; without
  `Reports` it throws `InvalidOperationException("The report address needs the Reports settings, Reports:Url and Reports:Secret, beside the client's.")`.
- Doc comments: `PreviousSecrets` says a report carries the secret its message was sent with, so a rotated secret
  stays listed until the reports of messages sent with it have come.

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: all pass, the existing 80 included.

- [ ] **Step 6: Build like CI and commit**

Run: `dotnet build Stabilt.TellusTalk.Client.slnx -c Release` and `dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`; both clean.

```bash
git add src/Stabilt.TellusTalk.Client tests/Stabilt.TellusTalk.Client.Tests
git commit -m "feat: hand out a report address that carries the report secret"
```

---

### Task 2: `DeliveryReport`

**Files:**
- Create: `src/Stabilt.TellusTalk.Client/DeliveryStatus.cs`, `DeliveryReport.cs`, `DeliveryReportJsonConverter.cs` (internal)
- Create: `tests/Shared/ReportBodies.cs`
- Modify: `tests/Directory.Build.props` (link `Shared/ReportBodies.cs` beside the licence file)
- Test: `tests/Stabilt.TellusTalk.Client.Tests/DeliveryReportTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum DeliveryStatus { Unknown, Delivered, Failed, Blocked }

[JsonConverter(typeof(DeliveryReportJsonConverter))]
public sealed record DeliveryReport
{
    public required string StatusText { get; init; }        // log_status
    public DeliveryStatus Status { get; }                     // from StatusText
    public int? StatusCode { get; init; }                     // log_status_code
    public string? FailedReason { get; init; }                // failed_reason, null when empty
    public string? FailedDetails { get; init; }               // failed_details, null when empty
    public string? To { get; init; }                          // to_address without "sms:"; another address whole
    public string? JobUuid { get; init; }                     // job_uuid
    public string? LogUuid { get; init; }                     // log_uuid
    public JsonElement? Echo { get; init; }                   // echo, null for JSON null or absent
    public int? Parts { get; init; }                          // quantity
    public DateTimeOffset? ReportTime { get; init; }          // report_time
}

// tests/Shared/ReportBodies.cs, namespace Stabilt.TellusTalk.Client.Tests
internal static class ReportBodies
{
    public const string Delivered = …;   // the emulator README's example, with "echo": {"appointment": 4711}
    public const string Failed = …;      // the same with txtDELIVERY_FAILED, 21 and txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None
    public const string Blocked = …;     // txtBLOCKED, 1, report_time null, failed_details "Duplicate of message 5d656a5cdafd11f1ba39a2551b38e481 sent within 5 minutes."
}
```

All three bodies use `to_address` `sms:+46701740605`, `job_uuid` `5d656a5cdafd11f1ba39a2551b38e481`, `log_uuid`
`5d656a5cdafd11f1ba39a2551b38e482`, `quantity` 1 and, where not null, `report_time` `2026-11-07T22:00:01.000Z`; copy
every field of the README's example (`E:\Stabilt\TellusTalk.Emulator\README.md`, "Delivery reports") so the reading is
tried against K8's whole body.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class DeliveryReportTests
{
    [Fact]
    public void A_delivered_report_is_read_with_its_fields()
    {
        var report = JsonSerializer.Deserialize<DeliveryReport>(ReportBodies.Delivered)!;

        report.Status.Should().Be(DeliveryStatus.Delivered);
        report.StatusText.Should().Be("txtDELIVERED");
        report.StatusCode.Should().Be(22);
        report.FailedReason.Should().BeNull();
        report.To.Should().Be("+46701740605");
        report.JobUuid.Should().Be("5d656a5cdafd11f1ba39a2551b38e481");
        report.LogUuid.Should().Be("5d656a5cdafd11f1ba39a2551b38e482");
        report.Echo!.Value.GetProperty("appointment").GetInt32().Should().Be(4711);
        report.Parts.Should().Be(1);
        report.ReportTime.Should().Be(new DateTimeOffset(2026, 11, 7, 22, 0, 1, TimeSpan.Zero));
    }

    [Fact]
    public void A_failed_report_is_read_with_its_reason() { /* Failed, 21, FailedReason "txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None" */ }

    [Fact]
    public void A_held_back_report_is_read_as_blocked() { /* Blocked, 1, ReportTime null, FailedDetails as in ReportBodies.Blocked */ }

    [Fact]
    public void A_status_the_client_does_not_know_is_read_as_unknown_with_its_text()
    { /* Delivered body with "log_status": "txtQUEUED", "log_status_code": 5 → Unknown, StatusText "txtQUEUED", StatusCode 5 */ }

    [Fact]
    public void An_address_that_is_not_an_sms_number_is_kept_whole()
    { /* "to_address": "email:reports@app.invalid" → To "email:reports@app.invalid" */ }

    [Fact]
    public void Optional_fields_of_another_type_are_read_as_missing()
    { /* "report_time": "yesterday", "log_status_code": "22", "quantity": "one" → ReportTime, StatusCode, Parts null; Status Delivered */ }

    [Theory]
    [InlineData("""{"to_address": "sms:+46701740605"}""")]
    [InlineData("""{"log_status": 22}""")]
    [InlineData("[]")]
    [InlineData("\"txtDELIVERED\"")]
    public void A_body_without_a_status_text_is_not_a_report(string body)
    {
        var read = () => JsonSerializer.Deserialize<DeliveryReport>(body);
        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void A_report_written_as_json_is_read_back_as_it_was()
    {
        // Serialize(Deserialize(ReportBodies.Failed)) and Deserialize again: every property equal, Echo by GetRawText.
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class "*DeliveryReportTests"`
Expected: the build fails; `DeliveryReport` does not exist.

- [ ] **Step 3: Implement the report, its status and its converter**

- `Status` maps `txtDELIVERED`, `txtDELIVERY_FAILED` and `txtBLOCKED` by ordinal comparison; anything else is `Unknown`.
- The converter reads the body as a `JsonElement`: a body that is not an object, or whose `log_status` is missing or not
  a string, throws `JsonException`. Every other field is taken only when it has the expected JSON type and, for
  `report_time`, parses as ISO 8601; otherwise it is `null`. `Echo` is a `Clone()` of the element.
- The converter writes K8's names for the properties it holds (`log_status`, `log_status_code`, `failed_reason`,
  `failed_details`, `to_address` as `To` is, `job_uuid`, `log_uuid`, `echo`, `quantity`, `report_time`), so a report an
  app passes on, such as in a queued message, reads back the same.
- The record's summary says the message's text, subject and html are left out on purpose: the app has them, and a
  report travels with personal data enough.

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: all pass.

- [ ] **Step 5: Build like CI and commit**

```bash
git add src/Stabilt.TellusTalk.Client tests/Shared tests/Directory.Build.props tests/Stabilt.TellusTalk.Client.Tests
git commit -m "feat: read tellustalk's delivery reports"
```

---

### Task 3: The `Stabilt.TellusTalk.Client.AspNetCore` package and its endpoint

**Files:**
- Create: `src/Stabilt.TellusTalk.Client.AspNetCore/Stabilt.TellusTalk.Client.AspNetCore.csproj`
- Create: `src/Stabilt.TellusTalk.Client.AspNetCore/ITellusTalkReportHandler.cs`
- Create: `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsServiceCollectionExtensions.cs`
- Create: `src/Stabilt.TellusTalk.Client.AspNetCore/TellusTalkReportsEndpointRouteBuilderExtensions.cs`
- Create: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Stabilt.TellusTalk.Client.AspNetCore.Tests.csproj`
- Create: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Support/ReportApp.cs`, `Support/ReceivedReports.cs`
- Test: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/ReportEndpointTests.cs`
- Modify: `Stabilt.TellusTalk.Client.slnx`, `Directory.Packages.props` (`Microsoft.AspNetCore.TestHost` `10.0.14`),
  `.github/workflows/ci.yml` (pack `Stabilt.TellusTalk.Client.slnx` instead of the one project)
- Create: `docs/decisions/20261123-receive-delivery-reports-at-an-endpoint-checked-by-a-secret.md`; modify
  `docs/decisions/README.md` (its row)

**Interfaces:**
- Consumes: `TellusTalkOptions.Reports`, `ReportAuthorization.IsValid` (Task 1); `DeliveryReport` (Task 2).
- Produces:

```csharp
namespace Stabilt.TellusTalk.Client.AspNetCore;
public interface ITellusTalkReportHandler
{
    Task HandleAsync(DeliveryReport report, CancellationToken cancellationToken);
}

namespace Microsoft.Extensions.DependencyInjection;
public static class TellusTalkReportsServiceCollectionExtensions
{
    public static IServiceCollection AddTellusTalkReports<THandler>(this IServiceCollection services)
        where THandler : class, ITellusTalkReportHandler;
}

namespace Microsoft.AspNetCore.Builder;
public static class TellusTalkReportsEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapTellusTalkReports(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern);
}

// tests: Support/ReceivedReports.cs
internal sealed class ReceivedReports { public ConcurrentQueue<DeliveryReport> Reports { get; } = new(); }
internal sealed class RecordingReportHandler(ReceivedReports received) : ITellusTalkReportHandler { … }
internal sealed class FailingReportHandler : ITellusTalkReportHandler { … throws InvalidOperationException … }

// tests: Support/ReportApp.cs — a WebApplication on TestServer with the client and the reports registered
internal static class ReportApp
{
    // Settings: Clients-style credentials, TellusTalk:Reports:Url https://app.invalid/sms/reports,
    // TellusTalk:Reports:Secret s3cret, TellusTalk:Reports:PreviousSecrets:0 0ld; `configure` may add or replace keys,
    // services and the mapping. Registers ReceivedReports as a singleton.
    public static Task<WebApplication> StartAsync<THandler>(
        Action<WebApplicationBuilder>? configure = null,
        Action<WebApplication>? map = null)            // default: app.MapTellusTalkReports("/sms/reports")
        where THandler : class, ITellusTalkReportHandler;

    public static Task<HttpResponseMessage> PostAsync(WebApplication app, string body, string? authorization = "Report s3cret", string path = "/sms/reports");
}
```

- [ ] **Step 1: Create both projects and add them to the solution**

The package's project follows `src/Stabilt.TellusTalk.Client/Stabilt.TellusTalk.Client.csproj`: `net10.0` with the same
comment, `IsPackable`, the same authors, licence, URLs, readme and `Version` `0.1.0`, `PackageTags`
`tellustalk;sms;delivery-reports;aspnetcore`, and the description `Receives TellusTalk's delivery reports in an ASP.NET
Core app: an endpoint that checks the report secret and hands each report to the app's handler.`; it references the
client project and `Microsoft.AspNetCore.App`. The test project references both source projects,
`Microsoft.AspNetCore.TestHost`, and the two emulator packages for Task 4.

- [ ] **Step 2: Write the failing endpoint tests**

```csharp
public sealed class ReportEndpointTests
{
    [Fact]
    public async Task A_report_with_the_secret_reaches_the_handler_and_is_answered_no_content()
    {
        await using var app = await ReportApp.StartAsync<RecordingReportHandler>();

        var response = await ReportApp.PostAsync(app, ReportBodies.Delivered);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Received(app).Should().ContainSingle().Which.To.Should().Be("+46701740605");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer s3cret")]
    [InlineData("Report wrong")]
    [InlineData("Report")]
    public async Task A_report_without_the_secret_is_turned_away_unhandled(string? authorization)
    { /* 401, Received empty */ }

    [Fact]
    public async Task A_report_with_a_previous_secret_is_taken() { /* "Report 0ld" → 204 */ }

    [Fact]
    public async Task A_secret_rotated_in_configuration_is_taken_without_a_restart()
    {
        // app.Configuration["TellusTalk:Reports:Secret"] = "rotated"; ((IConfigurationRoot)app.Configuration).Reload();
        // "Report rotated" → 204; "Report s3cret" → 401, since it is not among PreviousSecrets.
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{"to_address": "sms:+46701740605"}""")]
    public async Task A_body_that_is_not_a_report_is_answered_bad_request(string body) { /* 400, Received empty */ }

    [Fact]
    public async Task A_report_of_a_status_the_client_does_not_know_reaches_the_handler()
    { /* log_status "txtQUEUED" → 204, Status Unknown */ }

    [Fact]
    public async Task A_handler_that_fails_leaves_the_answer_to_the_apps_error_handling()
    {
        // StartAsync<FailingReportHandler>(map: app => { app.UseExceptionHandler(e => e.Run(_ => Task.CompletedTask)); app.MapTellusTalkReports("/sms/reports"); })
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task An_app_that_requires_authenticated_users_still_takes_reports()
    {
        // configure: AddAuthentication().AddCookie(); AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        // map: UseAuthentication(); UseAuthorization(); MapTellusTalkReports("/sms/reports")
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_endpoint_can_be_mapped_in_a_route_group()
    { /* map: app.MapGroup("/integrations").MapTellusTalkReports("/sms/reports"); post to /integrations/sms/reports → 204 */ }

    [Fact]
    public async Task Mapping_the_endpoint_without_the_reports_registration_is_refused()
    {
        // A WebApplication with AddTellusTalkClient alone: app.MapTellusTalkReports("/sms/reports")
        map.Should().Throw<InvalidOperationException>().WithMessage("*AddTellusTalkReports*");
    }

    [Fact]
    public async Task The_reports_registration_without_report_settings_fails_when_the_app_starts()
    {
        // TellusTalk:Reports:* removed from the settings; StartAsync
        await start.Should().ThrowAsync<OptionsValidationException>().WithMessage("*Reports*");
    }
}
```

`Received(app)` reads `app.Services.GetRequiredService<ReceivedReports>().Reports`.

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class "*ReportEndpointTests"`
Expected: the build fails; the extension methods do not exist.

- [ ] **Step 4: Implement the registration and the endpoint**

- `AddTellusTalkReports<THandler>` registers `THandler` as a scoped `ITellusTalkReportHandler` and adds
  `services.AddOptions<TellusTalkOptions>().Validate(options => options.Reports is not null, "TellusTalk's reports need the Reports settings, Reports:Url and Reports:Secret.")`;
  the start-up check `AddTellusTalkClient` registers runs it, and its skip during build-time OpenAPI generation applies.
- `MapTellusTalkReports` checks with `IServiceProviderIsService` that `ITellusTalkReportHandler` and `TellusTalkClient`
  are registered, and otherwise throws `InvalidOperationException("MapTellusTalkReports needs AddTellusTalkReports and AddTellusTalkClient in the app's services.")`.
  It maps `POST` on `pattern` and calls `.AllowAnonymous()` on it, with a comment saying the endpoint checks the secret
  itself so an app's fallback policy cannot turn TellusTalk away.
- The endpoint, in this order: reads `IOptionsMonitor<TellusTalkOptions>.CurrentValue.Reports` and
  `ReportAuthorization.IsValid(Authorization header)` → `Results.Unauthorized()` when not valid, before the body is read;
  `JsonSerializer.DeserializeAsync<DeliveryReport>(request.Body, cancellationToken: context.RequestAborted)` → a
  `JsonException` or `null` gives `Results.BadRequest()`; then the scoped handler from `context.RequestServices` with
  the report and `context.RequestAborted` → `Results.NoContent()`. A handler's exception is not caught.

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`
Expected: all pass.

- [ ] **Step 6: Pack both packages, and record the decision**

- `.github/workflows/ci.yml`'s pack step packs `Stabilt.TellusTalk.Client.slnx`, which packs both packages and skips the
  tests (`IsPackable` false). Run `dotnet pack Stabilt.TellusTalk.Client.slnx -c Release -o <scratch>` and check that
  two `.nupkg` come out and that the new one depends on `Stabilt.TellusTalk.Client` and has no ASP.NET Core package
  dependency.
- The decision record follows `docs/decisions/template.md`, titled `Receive delivery reports at an endpoint of a
  package of its own, checked by a secret in Authorization`. Its options and reasons come from the spec's
  "Approaches" and from its parts on `Authorization`, the scheme, the required secret, `PreviousSecrets` and the status
  codes; it names no app.

- [ ] **Step 7: Build like CI and commit**

```bash
git add Stabilt.TellusTalk.Client.slnx Directory.Packages.props .github/workflows/ci.yml docs/decisions src/Stabilt.TellusTalk.Client.AspNetCore tests/Stabilt.TellusTalk.Client.AspNetCore.Tests
git commit -m "feat: receive delivery reports at an endpoint that checks the report secret"
```

---

### Task 4: The whole path against the emulator, and the documentation

**Files:**
- Create: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/ReportsFromTheEmulatorTests.cs`
- Create: `tests/Stabilt.TellusTalk.Client.AspNetCore.Tests/Emulator/EmulatorReportApp.cs`
- Modify: `README.md`, `AGENTS.md`

**Interfaces:**
- Consumes: everything above; `TellusTalkEmulatorBuilder`, `TellusTalkEmulatorClient`, `MessageQuery` from the emulator
  packages.
- Produces: `EmulatorReportApp : IAsyncLifetime`, a class fixture holding a Kestrel app on `http://127.0.0.1:0` with
  `RecordingReportHandler`, the emulator, and the emulator's read API:

```csharp
public sealed class EmulatorReportApp : IAsyncLifetime
{
    public TellusTalkClient Client { get; }            // from the app's services
    public TellusTalkEmulatorClient Api { get; }
    public HttpClient EmulatorHttp { get; }            // for POST api/messages/{id}/reports
    public ReceivedReports Received { get; }
}
```

Its order is fixed by Testcontainers: start the app and read its port from `app.Urls`; call
`TestcontainersSettings.ExposeHostPortsAsync(port)`; build and start the emulator with
`.WithReportDelay(TimeSpan.FromHours(1))`; only then is `TellusTalk:BaseUrl` known. The app checks its settings when it
starts, before the port is known, so it starts with the credentials, the secret and a stand-in
`TellusTalk:Reports:Url` of `http://host.testcontainers.internal/sms/reports`; once the emulator runs, set
`TellusTalk:BaseUrl` to its address and `TellusTalk:Reports:Url` to
`http://host.testcontainers.internal:<port>/sms/reports` on `app.Configuration`, and `Reload()` it. The client reads
both through `IOptionsMonitor`, so the next client created and the next `ReportAddress` see them.

- [ ] **Step 1: Write the failing end-to-end tests**

```csharp
public sealed class ReportsFromTheEmulatorTests(EmulatorReportApp app) : IClassFixture<EmulatorReportApp>
{
    [Fact]
    public async Task A_delivered_report_from_the_emulator_reaches_the_handler()
    {
        var result = await app.Client.SendAsync(new SmsMessage
        {
            To = ["+46701740620"],
            Text = "Din tid är bekräftad.",
            ReportTo = [app.Client.ReportAddress],
            Echo = JsonSerializer.SerializeToElement(new { appointment = 4711 }),
        }, TestContext.Current.CancellationToken);

        var message = (await app.Api.GetMessagesAsync(new MessageQuery { To = "+46701740620" }, …)).Should().ContainSingle().Subject;
        message.Warnings.Should().BeEmpty();

        // POST api/messages/{message.Id}/reports with {"status":"delivered"} through app.EmulatorHttp, then wait for
        // app.Received to hold a report for +46701740620, polling for at most ten seconds.

        report.Status.Should().Be(DeliveryStatus.Delivered);
        report.LogUuid.Should().Be(result.LogUuid);
        report.Echo!.Value.GetProperty("appointment").GetInt32().Should().Be(4711);
    }

    [Fact]
    public async Task A_failed_report_from_the_emulator_reaches_the_handler()
    { /* +46701740621, {"status":"failed"} → Failed, FailedReason "txtFAILED_REASON: txtOPERATOR_ROUTING_ERROR, None" */ }
}
```

- [ ] **Step 2: Run them to see them fail, then make them pass**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx --filter-class "*ReportsFromTheEmulatorTests"`
Expected first: the build fails on the missing fixture. Write `EmulatorReportApp`; expected then: both pass. If the
emulator's report attempt shows a failure (its `reports[].outcome` on the message), read that before changing code: it
says whether the container reached the app.

- [ ] **Step 3: Document reports**

- `README.md` gains a section "Delivery reports" after "The message": the `Reports` settings, the second package's
  install line, `AddTellusTalkReports<THandler>()` and `MapTellusTalkReports(...)`, a handler, `ReportTo =
  [tellusTalk.ReportAddress]`, the four statuses, tying a report to its message by `Echo` or `LogUuid`, the status codes,
  rotating with `PreviousSecrets`, and that a handler should hand the report on quickly and be idempotent, since whether
  TellusTalk sends a report again is not known. Its intro says the client receives reports too.
- `AGENTS.md`: two packages instead of one, the new rows in the map, and the pack command on the solution.

- [ ] **Step 4: Run everything, build like CI and commit**

Run: `dotnet test --solution Stabilt.TellusTalk.Client.slnx`, `dotnet build Stabilt.TellusTalk.Client.slnx -c Release`,
`dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes`; all clean.

```bash
git add README.md AGENTS.md tests/Stabilt.TellusTalk.Client.AspNetCore.Tests
git commit -m "test: carry the emulator's delivery reports to the app's handler"
```

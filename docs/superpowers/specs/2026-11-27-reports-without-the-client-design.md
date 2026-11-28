# Receiving reports without the client's settings

## Intent

Where one app sends the messages and another receives their delivery reports, the receiving app has to hold K8's user
name and password today, and an absolute `Reports:Url`, though it uses neither. `MapTellusTalkReports` requires
`AddTellusTalkClient`, and the endpoint reads the secret from `TellusTalkOptions`, whose validation requires the
credentials. An app that receives reports should need only the secret and its route, and should not hold sending
credentials it never uses.

## Goals

- An app that only receives reports needs `TellusTalk:Reports:Secret`, `PreviousSecrets` during a rotation, and
  `MapTellusTalkReports(pattern)`; nothing else.
- The configuration works the same in one app that sends and receives as in two apps that split the work: the same
  section, the same keys, and the same arguments to `AddTellusTalkClient` and `AddTellusTalkReports`. The settings of an
  app that does both are the two apps' settings together.
- An app that only sends is unchanged.

## Non-goals

- Other section or key names for the receiving app.
- Checking that `Reports:Url` and a given route name the same endpoint. Behind a proxy that rewrites paths, neither app
  sees the proxy's configuration.

## Anti-goals

- A setting whose rules depend on which registration ran, invisible from the settings class: `UserName` required in one
  app and not in another.

## Constraints

- 0.2.1 is released. Breaking it is allowed under 0.x, marked `!` with a `BREAKING CHANGE:` footer, and released as
  0.3.0.
- Build-time OpenAPI generation starts the app without its secrets: the receiving side's settings skip
  `ValidateOnStart` there, as the client's do, and `MapTellusTalkReports()` still reads `Reports:Url` unvalidated.
- An accepted decision record is not edited. ADR 20261125-receive-delivery-reports-at-the-reports-url-and-answer-them-200
  names `AddTellusTalkReports<THandler>()` and the endpoint reading `TellusTalkOptions.Reports`, so a new record
  supersedes it.

## Approaches

1. **The receiving side reads its own settings.** `AddTellusTalkReports` binds the report settings from the same
   section the client reads, and validates only what the endpoint uses. The client's rules stay as they are, and the
   keys in configuration do not change; an app that does both passes its configuration to one more call.
2. **One settings class, with `UserName` and `Password` required only where `AddTellusTalkClient` is registered.** No new
   registration of settings, but the class's rules depend on which registration ran, which nothing in the class shows.
   The receiving app still has to have its section bound, so `AddTellusTalkReports` would take the configuration all
   the same.

The user chose 1 ("1) låter renare"), on the condition that the configuration works the same in one app as in two.

## Design

### Registration

- `AddTellusTalkReports<THandler>` takes the arguments `AddTellusTalkClient` takes:
  - `(IConfiguration configuration, string sectionName = "TellusTalk")`, searching beneath a configuration passed as an
    `IConfiguration`, as the client does;
  - `(IConfigurationSection section)`, the `TellusTalk` section itself rather than its `Reports`;
  - `(Action<TellusTalkReportOptions> configureOptions)`, setting the report settings alone.
- The overload without arguments goes.
- It binds `TellusTalkReportOptions` from the section's `Reports` and validates it when the app starts:
  - `Secret` required, and visible ASCII without spaces;
  - `PreviousSecrets` without an empty secret or one that is not visible ASCII;
  - `Url`, when set, an absolute http or https URL.

  `BaseUrl`, `UserName` and `Password` are not read.
- `MapTellusTalkReports` requires `AddTellusTalkReports` alone.

### The endpoint

- The filter checks the secret against the receiving side's settings through `IOptionsMonitor`, so a rotated secret
  still needs no restart.
- `MapTellusTalkReports()` reads `Reports:Url` from the receiving side's settings, unvalidated, and is refused as today
  when it is missing or not absolute.

### The sending side

- `AddTellusTalkClient` and `TellusTalkOptions.Reports` are unchanged: when `Reports` is set, `Url` and `Secret` are
  required. One class, `TellusTalkReportOptions`, describes `Reports` in both apps; an app that only sends never reads
  its `PreviousSecrets`.

### Settings from code

- An app that does both and sets its settings from code gives the secret to both calls: the client's `options => …`
  sets `Reports` with it, and the reports' `reports => …` sets it again. From configuration, the one key serves both.

### Rotation

- `Secret` and `PreviousSecrets` work as today. With two apps, as with several instances of one: first list the new
  secret in `PreviousSecrets` in the receiving app, then make it `Secret` everywhere with the old one in
  `PreviousSecrets`, and remove the old one once the reports of messages sent with it have come.

### Documentation

- The README's delivery reports:
  - `AddTellusTalkReports<SmsReportHandler>(builder.Configuration)` in the example;
  - a table of its calls, as the client has;
  - an app that only receives, with its settings and its route;
  - the rotation paragraph across apps as well as instances;
  - the sentence that `AddTellusTalkReports` requires `Reports`, which now requires `Reports:Secret`.
- The XML documentation of the members that change.
- A new decision record superseding ADR 20261125, restating it with the receiving side's own settings; its follow-up
  is answered.

### Tests

- An app with `AddTellusTalkReports` and only `Reports:Secret` starts, without `BaseUrl`, `UserName`, `Password` or
  `Reports:Url`. A report with the secret reaches its handler; one without is answered `401`.
- In that app, `MapTellusTalkReports()` without a route is refused, since `Reports:Url` is missing.
- The receiving app fails at start without `Reports:Secret`, with a relative `Reports:Url`, and with a bad secret in
  `PreviousSecrets`.
- A section name and a section passed reach the same settings as for the client.
- One app with both registrations reads one set of settings: the secret the client sends is the one the endpoint takes.
- `MapTellusTalkReports` without `AddTellusTalkReports` is refused; without `AddTellusTalkClient` it is not.
- The existing endpoint tests move to the new call.

## Left to the builder

- Whether the receiving side's settings are the default `TellusTalkReportOptions` or a named instance.
- Where the validation of a secret, shared by both sides, is written once.
- The decision record's identity and wording, and the tests' names and places.

## Calls worth a look

1. **The code overload takes `TellusTalkReportOptions`,** so an app that does both from code gives the secret twice.
2. **`Url` is optional on the receiving side, but validated when set,** so a wrong URL fails at start rather than at
   `MapTellusTalkReports()`.
3. **A new decision record supersedes ADR 20261125,** restating the whole decision as this repository does.
4. **The version is 0.3.0,** for the removed overload of `AddTellusTalkReports`.

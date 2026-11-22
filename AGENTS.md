# TellusTalk Client

A typed .NET client for TellusTalk's K8 send API: an app registers it with `AddTellusTalkClient`, builds an
`SmsMessage`, and sends it. .NET 10 and later, `IHttpClientFactory`, one NuGet package.

## Read first

- [README.md](README.md): what it does, the message's properties and their rules, and how to point it at the emulator.
- [docs/decisions/README.md](docs/decisions/README.md): why things are the way they are.
- [docs/conventions.md](docs/conventions.md): how code and tests are written here.

## Never

- **Retry a send.** A send that failed in transit may have reached TellusTalk, and a retry sends the SMS twice.
- **Let a message through that TellusTalk's specification forbids** where the client can tell; refuse it with an
  `ArgumentException` where the value is set.
- **Normalise a value silently,** such as a national number into `+46…`. What the app passes is what is sent, or it is
  refused.
- **Pass for TellusTalk's own product.** The project is not affiliated with TellusTalk.
- **Borrow models from the emulator.** The client reads the specification on its own, so the emulator's tests of it
  can catch its mistakes.
- **Write personal data or a TellusTalk customer's name** into code, tests, logs or documents. Test numbers come from
  PTS's range for fiction, `+46701740605` to `+46701740699`.

## Map

| Path | What |
|---|---|
| `src/Stabilt.TellusTalk.Client` | The client, the message and its parts, the options and `AddTellusTalkClient` |
| `tests/Stabilt.TellusTalk.Client.Tests` | The requests the client sends and its refusals, against `RecordingHandler` |
| `tests/Stabilt.TellusTalk.Client.Tests/Emulator` | The same requests sent to TellusTalk Emulator in a container, which must hold them without warnings |
| `.github/workflows/ci.yml` | Build, format check and tests; a `v*` tag publishes the package |

## Commands

| Task | Command |
|---|---|
| Build like CI | `dotnet build Stabilt.TellusTalk.Client.slnx -c Release` |
| Format check | `dotnet format Stabilt.TellusTalk.Client.slnx --verify-no-changes` |
| Test | `dotnet test --solution Stabilt.TellusTalk.Client.slnx` (Microsoft Testing Platform, through `global.json`; needs Docker) |
| Pack | `dotnet pack src/Stabilt.TellusTalk.Client -c Release` |

## Traps

- Debug builds hide analyzer errors. Build `-c Release` before pushing.
- The package targets `net10.0` and the 10.0.0 Microsoft.Extensions packages; an API newer than .NET 10 does not
  compile, and raising a package's version raises it for every app.
- `JsonContent` streams its body chunked; the client serialises the body first so the request carries its length.
- The emulator's image is the version of `Stabilt.TellusTalk.Emulator.Testcontainers` in `Directory.Packages.props`;
  raise it together with `Stabilt.TellusTalk.Emulator.Client`, as a change of its own.

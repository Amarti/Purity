# Calculation parity tests

These tests prove the port gives exactly the same dates as the production engine.

| Project | Purpose |
|---|---|
| `Purity.Reference.V1` | Frozen copy of the production engine. Never edit it |
| `Purity.Parity` | Compares `src/Purity.Core` with the reference, approved files and the real history |
| `oracle/` | The compiled 2022 production `Purity.dll`, used to prove the frozen source behaves the same |
| `Purity.Storage.Tests` | Proves saving and merging can't lose the history, by making each failure happen on purpose |
| `Purity.Desktop.Tests` | The desktop app's behavior through its view models: what it saves, what it asks, and that it never writes over a file it couldn't read |

For screenshots or a side-by-side run of the apps, `dotnet run tools/make-sample-data.cs -- <file>` writes a made-up history. Point a test run at it with `--data <file>`, and set `PURITY_APP_DATA` to a scratch folder so the app's own settings and local copies stay out of the real ones.

## Running

```bash
dotnet run -f net10.0 --project tests/Purity.Parity/Purity.Parity.csproj
```

```bash
dotnet run -f net10.0 --project tests/Purity.Storage.Tests/Purity.Storage.Tests.csproj
```

Tests that need real data skip when `tests/private/` is missing. Set `PURITY_REQUIRE_REAL_DATA=1` to make them fail instead, which release builds must do.

## On machines CI can't reach

Hosted CI has no Monterey Mac and no real Debian machine, so `tools/platform-check` builds a kit that needs nothing installed: both suites on .NET 10 and .NET 8, plus a small window for each UI candidate, all self-contained. The person at the machine runs one script and sends back the report. The kit's README explains how.

```bash
bash tools/platform-check/build-kit.sh                  # Mac, Intel and Apple Silicon
```

```bash
bash tools/platform-check/build-kit.sh linux-x64 linux-arm64
```

The kits land in `out/platform-check`. Without ICU, as on a minimal Linux install, the kit runs with globalization off and most calculation tests skip, so install `libicu` first for a full run.

The first run, in November 2026 on an Intel MacBook Pro with macOS 12.7.6, passed everything for all three candidates, so Monterey gets the main Avalonia 12 build on .NET 10. Apple Silicon on Monterey hasn't been tried. Rerun the kit on a Monterey Mac after upgrading Avalonia or .NET.

Between runs, CI publishes the Mac build every night and on each push, and fails if any program or library in it needs a newer macOS than 12.0:

```bash
dotnet run tools/check-macos-minimum.cs -- <publish folder> 12.0
```

That catches the usual way an update drops a macOS version. It can't catch a library calling something newer only at run time, which is why the kit is still the real test.

## Real data

`tests/private/` holds a snapshot of `pureData.json` and older copies, and it is gitignored. The repo is public, so only the encrypted file `tests/fixtures/private.tar.gz.gpg` may be committed. Use `tools/fixture.sh` to create or unpack it. Once that file is committed, CI fails if the `PURITY_FIXTURE_PASSPHRASE` secret is missing, so the real-data tests can't quietly stop running. Forks and fork pull requests skip them.

Keep real dates out of test code, commit messages and CI output. Real-data failures report a position only, and public edge-case tests use synthetic dates of the same kind. Never compare real periods or file content with `Assert.Equal`, because a failure prints both values.

## Approved output

`Purity.Parity/Golden/` holds output approved from the reference on Windows. Run with `PURITY_UPDATE_GOLDEN=1` to regenerate it.

A golden file changes only with a bump of `PeriodLedger.RulesVersion` and a reviewed list of every changed date. Halachic changes also need the rav's sign-off recorded. CI rejects a golden change without the bump.

## Mutation testing

`stryker-config.json` deliberately breaks `Purity.Core` in small ways and checks that a test notices each one:

```bash
cd tests/Purity.Parity
dotnet stryker --config-file stryker-config.json
```

Read the JSON report, not the printed summary. Stryker counts a timeout as caught, and with this test runner a timeout can mean nothing ran: in the first run, 19 timeouts hid real gaps, including an untested method. Apply each timed-out change by hand and confirm a test fails. The summary table also lists skipped mutants under "survived".

`Purity.Storage.Tests` has its own `stryker-config.json`, run the same way. Four more things to check there:

- Search the log for `Safe Mode`. Stryker gives up on a whole method when it can't compile a mutant, then reports those mutants as compile errors. Declaring a variable before a `try` and assigning it inside causes this, and it once removed the entire merge logic from a run without lowering the score.
- Its threshold is lower than the engine's because Windows can't run the Unix permission code, and some lines can't be tested: the disk flush, `Dispose`, overlapping watcher events and message wording. Run the Unix-only mutants on Linux by hand.
- Run it on a copy with `CrashTests.cs` removed. Those tests start and kill real processes, which says nothing about a single mutant, and with them a run didn't finish in 45 minutes. Without them it takes about an hour.
- Timeouts need the same hand check. In one run, 18 of 48 were changes no test noticed and 3 hung the whole suite, which only the time limit on the CI step catches.

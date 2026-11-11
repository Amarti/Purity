# Calculation parity tests

These tests hold the engine to the dates the old production engine gave, which are kept as approved output in the repo.

| Project | Purpose |
|---|---|
| `Purity.Parity` | Checks `src/Purity.Core` against approved files, hand-written edge cases and the real history |
| `Purity.Storage.Tests` | Proves saving and merging can't lose the history, by making each failure happen on purpose |
| `Purity.Desktop.Tests` | The desktop app's behavior through its view models: what it saves, what it asks, and that it never writes over a file it couldn't read |

For screenshots or a side-by-side run of the apps, `dotnet run tools/make-sample-data.cs -- <file>` writes a made-up history. Point a test run at it with `--data <file>`, and set `PURITY_APP_DATA` to a scratch folder so the app's own settings and local copies stay out of the real ones.

## Running

```bash
dotnet run --project tests/Purity.Parity/Purity.Parity.csproj
```

```bash
dotnet run --project tests/Purity.Storage.Tests/Purity.Storage.Tests.csproj
```

Tests that need real data skip when `tests/private/` is missing. Set `PURITY_REQUIRE_REAL_DATA=1` to make them fail instead, which release builds must do.

## On machines CI can't reach

Hosted CI has no Monterey Mac and no real Debian machine, so `tools/platform-check` builds a kit that needs nothing installed: the three test suites and the desktop app (a `Purity.app` bundle on a Mac), all self-contained on .NET 10. The script runs the suites, has the app save a picture of its window with a made-up history (`--picture`, which refuses to run without `--data`), then lets the person try the app and answer a few questions, and packs up a report to send back. The kit's README explains how. The .NET 8 build of the engine is still tested in CI, but the kit no longer carries it, since Monterey runs .NET 10.

```bash
bash tools/platform-check/build-kit.sh                  # Mac, Intel and Apple Silicon
```

```bash
bash tools/platform-check/build-kit.sh linux-x64 linux-arm64
```

The kits land in `out/platform-check`. Without ICU, as on a minimal Linux install, the tests run with globalization off and most calculation tests skip, so install `libicu` first for a full run. The app itself never needs ICU.

The first run, in November 2026 on an Intel MacBook Pro with macOS 12.7.6, passed everything for all three candidates, so Monterey gets the main Avalonia 12 build on .NET 10. Apple Silicon isn't a Monterey target: it is checked on the latest macOS only, and CI runs every suite on an Apple Silicon Mac. Rerun the kit on the Monterey Mac after upgrading Avalonia or .NET; it passed again in November 2026 with Avalonia 12.1.5.

Between runs, CI publishes the Mac build every night and on each push, and fails if any program or library in it needs a newer macOS than 12.0:

```bash
dotnet run tools/check-macos-minimum.cs -- <publish folder> 12.0
```

That catches the usual way an update drops a macOS version. It can't catch a library calling something newer only at run time, which is why the kit is still the real test.

Google Calendar can't be reached from CI either, so the tests use a local stand-in for it. `tools/google-check` builds a kit that tries reminders against the real Google Calendar: the app with the project's Google client ID and secret built in, a made-up history whose reminders fall in the coming weeks, and a checklist covering sign-in, the calendar, an alert on the phone, a language change, a new period, a second computer and removing everything. It asks for the ID and secret, so they never reach the repo, and builds outside the repo's own build folders:

```bash
dotnet run tools/google-check/build-kit.cs
```

The kits land in `out/google-check`, and each kit's README explains how to use it. In November 2026 every check passed on Windows and on the Intel Mac. Linux hasn't been tried.

For Linux, `bash tools/package-linux.sh` builds `out/linux/purity_<version>_<arch>.deb` for amd64 and arm64 (on Windows its last step runs in WSL). The Linux package workflow installs each package in clean Debian 12 and 13 and Ubuntu 22.04 and 24.04 containers, then opens the app on a made-up history on a virtual screen, which proves the package's dependencies are enough. `tools/test-linux-package.sh` does the same locally in a Docker container.

## Real data

`tests/private/` holds a snapshot of `pureData.json` and older copies, and it is gitignored. The repo is public, so only the encrypted file `tests/fixtures/private.tar.gz.gpg` may be committed. Use `tools/fixture.sh` to create or unpack it. Once that file is committed, CI fails if the `PURITY_FIXTURE_PASSPHRASE` secret is missing, so the real-data tests can't quietly stop running. Forks and fork pull requests skip them.

Keep real dates out of test code, commit messages and CI output. Real-data failures report a position only, and public edge-case tests use synthetic dates of the same kind. Never compare real periods or file content with `Assert.Equal`, because a failure prints both values.

## Approved output

`Purity.Parity/Golden/` holds output approved from the old production engine, before it was retired. Run with `PURITY_UPDATE_GOLDEN=1` to regenerate it from the current engine, then review every changed date.

A golden file changes only with a bump of `PeriodLedger.RulesVersion` and a reviewed list of every changed date. Halachic changes also need the rav's sign-off recorded. CI rejects a golden change without the bump; adding a new golden file doesn't count as a change.

`Golden/self-test.txt` is what the apps check themselves against at every start. `Purity.Core` embeds it, and `SelfTest.Run()` calculates the same 13 made-up histories and compares. If anything differs, the desktop app shows no dates and doesn't open the data file. The histories reach every rule and the known issues H1 to H4; `SelfTestTests` proves that the engine and the embedded copy match it.

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

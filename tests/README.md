# Calculation parity tests

These tests prove the port gives exactly the same dates as the production engine.

| Project | Purpose |
|---|---|
| `Purity.Reference.V1` | Frozen copy of the production engine. Never edit it |
| `Purity.Parity` | Compares `src/Purity.Core` with the reference, approved files and the real history |
| `oracle/` | The compiled 2022 production `Purity.dll`, used to prove the frozen source behaves the same |

## Running

```bash
dotnet run -f net10.0 --project tests/Purity.Parity/Purity.Parity.csproj
```

Tests that need real data skip when `tests/private/` is missing. Set `PURITY_REQUIRE_REAL_DATA=1` to make them fail instead, which release builds must do.

## Real data

`tests/private/` holds a snapshot of `pureData.json` and older copies, and it is gitignored. The repo is public, so only the encrypted file `tests/fixtures/private.tar.gz.gpg` may be committed. Use `tools/fixture.sh` to create or unpack it. Once that file is committed, CI fails if the `PURITY_FIXTURE_PASSPHRASE` secret is missing, so the real-data tests can't quietly stop running. Forks and fork pull requests skip them.

Keep real dates out of test code, commit messages and CI output. Real-data failures report a position only, and public edge-case tests use synthetic dates of the same kind.

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

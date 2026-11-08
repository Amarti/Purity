# Purity platform check

This kit checks whether the new Purity app can run on this computer, before the app is built. It
runs the calculation tests, the data file tests and three candidate versions of the app window,
then writes a report to send back. The windows show made-up dates only. Your data file is never
read or changed.

## Which kit

- Mac with an Apple chip (Apple menu > About This Mac says "Chip: Apple M..."): `purity-check-osx-arm64`
- Mac with an Intel processor (it says "Processor: ... Intel ..."): `purity-check-osx-x64`
- Linux: `purity-check-linux-x64` or `purity-check-linux-arm64`, if one was built

## Running it

1. Unzip the kit. On a Mac, double-click the zip.
2. Open Terminal (Applications > Utilities > Terminal).
3. Type `bash` and a space, drag `run-check.sh` from the kit folder into the Terminal window, and
   press Return.
4. Wait about 5 to 15 minutes. Windows flash up briefly, which is expected. If macOS asks whether
   Terminal may use a folder, allow it.
5. At the end a window opens for you to try, followed by a few yes or no questions.
6. Send back the results file named on the last line.

Nothing is installed: deleting the kit folder removes everything. The kit is built on Windows, so
its programs arrive unsigned; on a Mac the script gives them an ad hoc signature, which is valid on
that Mac only.

## Optional: real data

To include the real-data checks, copy the `tests/private` folder from the Windows repo into the kit
folder, named `private`. The tests work on a temporary copy. That folder is your history in
readable form, so delete it when the check is done.

## What it checks

- Calculations: the whole parity suite on .NET 10 and on .NET 8, comparing the new engine with the
  frozen production engine and the approved output.
- Data file: the whole storage suite on both runtimes, covering atomic saves, merging, locks and
  recovery after a crash.
- Windows: Avalonia 12 on .NET 10, Avalonia 12 on .NET 8, and Avalonia 11.3 on .NET 8. Each opens,
  shows the engine's date text, saves a picture of itself and closes.

The first combination whose tests and window both pass becomes the build for this kind of machine.

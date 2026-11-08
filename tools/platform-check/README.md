# Purity platform check

This kit checks the new Purity app on this computer. It runs the calculation, data file and app
tests, opens the app with a made-up history, and writes a report to send back. Your data file is
never read or changed.

## Which kit

- Mac with an Apple chip (Apple menu > About This Mac says "Chip: Apple M..."): `purity-check-osx-arm64`
- Mac with an Intel processor (it says "Processor: ... Intel ..."): `purity-check-osx-x64`
- Linux: `purity-check-linux-x64` or `purity-check-linux-arm64`, if one was built

## Running it

1. Unzip the kit. On a Mac, double-click the zip.
2. Open Terminal (Applications > Utilities > Terminal).
3. Type `bash` and a space, drag `run-check.sh` from the kit folder into the Terminal window, and
   press Return.
4. Wait a few minutes. A window flashes up, which is expected. If macOS asks whether Terminal may
   use a folder, allow it.
5. At the end the app opens for you to try, followed by a few yes or no questions.
6. Send back the results file named on the last line.

Nothing is installed: deleting the kit folder removes everything. The kit is built on Windows, so
its programs aren't signed by an Apple developer; on a Mac the script prepares them to run on that
Mac only.

## Trying the app again

After the check has run once, double-click `try-purity.command` (on Linux, run
`bash try-purity.command`). It opens the app with the made-up history and keeps everything the app
writes in the `try` folder. Delete that folder to start again from the original history.

Please don't open `Purity.app` directly and point it at your real data file. This is a test
version: it would start saving to that file.

## Optional: real data

To include the real-data tests, copy the `tests/private` folder from the Windows repo into the kit
folder, named `private`. The tests work on a temporary copy. That folder is your history in
readable form, so delete it when the check is done. The app itself only ever opens the made-up
history.

## What it checks

- Calculations: the whole parity suite, comparing the new engine with the frozen production
  engine and the approved output.
- Data file: the whole storage suite, covering atomic saves, merging, locks and recovery after a
  crash.
- The app: its tests, a picture of its window with the made-up history, and your hands-on check
  of scrolling, the calendar, accepting a period and the file chooser.

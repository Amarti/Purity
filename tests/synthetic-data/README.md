# Made-up data for trying the app

Git ignores everything here except this file, and only invented histories belong here.
Visual Studio's "Purity (synthetic data)" profile runs the app on `sample.json` in this folder and
keeps its settings and backups in `app data`, so it never touches your real ones.

To make a history, from the root of the repository:

    dotnet run tools/make-sample-data.cs -- tests/synthetic-data/sample.json

The periods fall in 2030 and 2031. To have them elsewhere, add when the first one begins, as
`2010-01-06`, after the file's name.

Delete `app data` to go back to the default window size and settings. Make a new `sample.json` to
go back to a new history.

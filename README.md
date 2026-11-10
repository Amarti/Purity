# Purity

## Jewish family purity tracker

## Building the desktop app

One command builds the app for Windows, macOS and Linux into `out/<version>/`. Each build is self-contained, so the computer it goes to needs nothing installed:

```bash
bash tools/build-apps.sh
```

To build only some, name them: `bash tools/build-apps.sh windows mac`. It needs the .NET 10 SDK; on Windows, run it in Git Bash, and the Linux packages also need WSL. The version is set in one place, `<Version>` in `src/Purity.Desktop/Purity.Desktop.csproj`, and everything takes it from there. Reminders through Google Calendar need the project's Google client ID and secret in `PURITY_GOOGLE_CLIENT_ID` and `PURITY_GOOGLE_CLIENT_SECRET` when building. They must never be committed, and a build without them simply doesn't offer Google.

| File | For | To install and run |
|---|---|---|
| `Purity-<version>-windows-x64.exe` | Windows PCs | The whole app in one file: put it anywhere, such as the desktop, and run it. It isn't code-signed, so Windows may warn the first time: More info, then Run anyway |
| `Purity-<version>-windows-arm64.exe` | Windows on ARM | The same |
| `Purity-<version>-macos-intel.zip` | Macs with an Intel processor, macOS 12 and later | Unzip and move `Purity.app` to Applications. It isn't signed by an Apple developer, so a downloaded copy is blocked the first time: right-click it and choose Open, or on macOS 15 and later allow it in System Settings, Privacy & Security. Running `xattr -dr com.apple.quarantine /Applications/Purity.app` once in Terminal also works. A copy brought over by Dropbox or a USB stick isn't blocked |
| `Purity-<version>-macos-apple-silicon.zip` | Macs with an Apple chip | The same |
| `purity_<version>_amd64.deb` | Debian and Ubuntu PCs | `sudo apt install ./purity_<version>_amd64.deb`, then Purity is in the menu. `sudo apt remove purity` removes it |
| `purity_<version>_arm64.deb` | Debian and Ubuntu on ARM | The same |

The app keeps its settings and local copies per user: in `%LOCALAPPDATA%\Purity` on Windows, `~/Library/Application Support/Purity` on macOS and `~/.local/share/Purity` on Linux.

Current TODOs:

 - Add options to the settings window:
	- Or Zarua option
 - Settle the open calculation questions with a rav, then apply the answers
 - Desktop (Windows, macOS, Linux)
	- reminders through iCloud and other CalDAV, then Outlook (Google is done)
	- publish the Google project before relying on Google reminders: sign-ins of a project in Testing expire after 7 days
	- check Google reminders on Linux by hand (Windows and macOS passed)
	- check the current look by hand on macOS and Linux
 - Port to iOS
	- external storage file path (dropbox/google drive/iCloud storage integration)
	- calendar/reminders integration
	- Apple Watch viewer
 - Port to android
	- external storage file path (dropbox/google drive integration)
	- calendar/reminders integration

# Purity

## Jewish family purity tracker

## Building the desktop app

One command builds the app for Windows, macOS and Linux into `out/<version>/`. Each build is self-contained, so the computer it goes to needs nothing installed:

```bash
dotnet run tools/build-apps.cs
```

It needs only the .NET 10 SDK and runs from any console: Command Prompt, PowerShell, or a Mac or Linux terminal, with no git or Git Bash, so a downloaded copy of the code builds too. To build only some, name them: `dotnet run tools/build-apps.cs -- windows mac`. The Linux packages are made by `dpkg-deb`, which only Linux has: on Windows that step runs in WSL, and a Mac can't build them. The version is set in one place, `<Version>` in `src/Version.props`, and everything takes it from there. Reminders through Google Calendar need the project's Google client ID and secret built in: the script asks for them, or takes them from `PURITY_GOOGLE_CLIENT_ID` and `PURITY_GOOGLE_CLIENT_SECRET`, and pressing Enter at the ID builds without Google. They must never be committed, and a build without them doesn't offer Google. iCloud and other CalDAV calendars need nothing built in: each person types their own sign-in in the Reminders window.

| File | For | To install and run |
|---|---|---|
| `Purity-<version>-windows-x64.exe` | Windows PCs | The whole app in one file: put it anywhere, such as the desktop, and run it. It isn't code-signed, so Windows may warn the first time: More info, then Run anyway |
| `Purity-<version>-windows-arm64.exe` | Windows on ARM | The same |
| `Purity-<version>-macos-intel.zip` | Macs with an Intel processor, macOS 12 and later | Unzip and move `Purity.app` to Applications. It isn't signed by an Apple developer, so a downloaded copy is blocked the first time: right-click it and choose Open, or on macOS 15 and later allow it in System Settings, Privacy & Security. Running `xattr -dr com.apple.quarantine /Applications/Purity.app` once in Terminal also works. A copy brought over by Dropbox or a USB stick isn't blocked |
| `Purity-<version>-macos-apple-silicon.zip` | Macs with an Apple chip | The same |
| `purity_<version>_amd64.deb` | Debian and Ubuntu PCs | `sudo apt install ./purity_<version>_amd64.deb`, then Purity is in the menu. `sudo apt remove purity` removes it |
| `purity_<version>_arm64.deb` | Debian and Ubuntu on ARM | The same |

The app keeps its settings and local copies per user: in `%LOCALAPPDATA%\Purity` on Windows, `~/Library/Application Support/Purity` on macOS and `~/.local/share/Purity` on Linux.

## Building the Android app

The Android app shares everything but its start with the desktop app: the views and view models are in `src/Purity.UI`, and `src/Purity.Android` holds only its activity, the phone's secure store, Google's Android sign-in and the icons. Phones get a compact layout and stay upright; tablets get the desktop's layout and turn either way. The history is a file the person picks in Android's picker, such as `pureData.json` in Google Drive, and reminders go to Google Calendar or another CalDAV server.

Building it needs the .NET Android workload, the Android SDK and a JDK. Visual Studio's ".NET Multi-platform App UI development" or "Mobile development with .NET" option installs all three; without Visual Studio, `dotnet workload install android` adds the workload, and `dotnet build src/Purity.Android -t:InstallAndroidDependencies -p:AcceptAndroidSDKLicenses=True` fetches the SDK parts. Nothing has to be installed on the phone. To try it on a phone:

1. On the phone, turn on Developer options (Settings, About phone, tap the build number seven times), then USB debugging in them. Android 11 and later can do Wireless debugging instead.
2. Connect it by USB and allow this computer when the phone asks. `adb devices` (in the Android SDK's `platform-tools`) should list it. Some Windows PCs need the phone maker's USB driver first; Pixels use Google's.
3. In Visual Studio, choose `Purity.Android` and the phone, and run it. From a console: `dotnet build src/Purity.Android -t:Run -c Release`.

`dotnet build src/Purity.Android -c Release` also leaves an installable `io.github.amarti.purity-Signed.apk` in `src/Purity.Android/bin/Release/net10.0-android/`, which `adb install -r` puts on any phone that allows apps from unknown sources. It's signed with this computer's debug key: a phone that has a copy signed by another key must uninstall that first, which also removes the app's settings and backups there.

Reminders through Google need an Android client in the same Google Cloud project as the desktop's, under Google Auth Platform, Clients: the package name `io.github.amarti.purity` and the SHA-1 of the key the app is signed with. For builds from this computer that's the debug key's, which `keytool -list -v -keystore "%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore" -alias androiddebugkey -storepass android` shows (`~/.local/share/Xamarin/Mono for Android/debug.keystore` on a Mac or Linux). Nothing is built into the app for it, and a phone without Google Play services doesn't offer Google.

What's done, what's next and what's waiting: see [PROGRESS.md](PROGRESS.md).

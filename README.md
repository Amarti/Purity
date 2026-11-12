# Purity

## Jewish family purity tracker

## Building the desktop app

One command builds the app for Windows, macOS and Linux into `out/<version>/`. Each build is self-contained, so the computer it goes to needs nothing installed:

```bash
dotnet run tools/build-apps.cs
```

It needs only the .NET 10 SDK and runs from any console: Command Prompt, PowerShell, or a Mac or Linux terminal, with no git or Git Bash, so a downloaded copy of the code builds too. To build only some, name them: `dotnet run tools/build-apps.cs -- windows mac`. The Linux packages are made by `dpkg-deb`, which only Linux has: on Windows that step runs in WSL, and a Mac can't build them. The version is set in one place, `<Version>` in `src/Purity.Desktop/Purity.Desktop.csproj`, and everything takes it from there. Reminders through Google Calendar need the project's Google client ID and secret built in: the script asks for them, or takes them from `PURITY_GOOGLE_CLIENT_ID` and `PURITY_GOOGLE_CLIENT_SECRET`, and pressing Enter at the ID builds without Google. They must never be committed, and a build without them doesn't offer Google.

| File | For | To install and run |
|---|---|---|
| `Purity-<version>-windows-x64.exe` | Windows PCs | The whole app in one file: put it anywhere, such as the desktop, and run it. It isn't code-signed, so Windows may warn the first time: More info, then Run anyway |
| `Purity-<version>-windows-arm64.exe` | Windows on ARM | The same |
| `Purity-<version>-macos-intel.zip` | Macs with an Intel processor, macOS 12 and later | Unzip and move `Purity.app` to Applications. It isn't signed by an Apple developer, so a downloaded copy is blocked the first time: right-click it and choose Open, or on macOS 15 and later allow it in System Settings, Privacy & Security. Running `xattr -dr com.apple.quarantine /Applications/Purity.app` once in Terminal also works. A copy brought over by Dropbox or a USB stick isn't blocked |
| `Purity-<version>-macos-apple-silicon.zip` | Macs with an Apple chip | The same |
| `purity_<version>_amd64.deb` | Debian and Ubuntu PCs | `sudo apt install ./purity_<version>_amd64.deb`, then Purity is in the menu. `sudo apt remove purity` removes it |
| `purity_<version>_arm64.deb` | Debian and Ubuntu on ARM | The same |

The app keeps its settings and local copies per user: in `%LOCALAPPDATA%\Purity` on Windows, `~/Library/Application Support/Purity` on macOS and `~/.local/share/Purity` on Linux.

What's done, what's next and what's waiting: see [PROGRESS.md](PROGRESS.md).

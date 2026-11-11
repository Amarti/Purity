# Progress

What's done, what's next, and what's waiting on whom. This file is updated in the same commit as
every change that finishes, starts, drops or blocks a stage.

✅ done · 🔨 built, not released yet · ⬜ to do · ⏳ waiting on you · ✖️ dropped

**Now:** version 2.4.2, on the `calendar-reminders` branch, waiting to be merged into `master`.

## Done

| | Area | What | Version |
|---|---|---|---|
| ✅ | Calculations | The engine moved into `Purity.Core` and proven to match the old app: the whole stored history, every date from 1900 to 2100 in both onot, random histories and old snapshots. CI checks it on Windows, Linux and macOS, on Intel and ARM | 2.1 |
| ✅ | Storage | Safe saving: atomic writes, merging a change made on another device, locks, recovery after a crash. Local backups with "Restore earlier version", moving the file to a synced folder, and picking up changes made elsewhere | 2.1 |
| ✅ | Desktop | Windows as one `Purity.exe`, macOS 12 and later (Intel and Apple Silicon), Debian and Ubuntu as a `.deb`. Runs on Monterey, checked on a real Mac | 2.1 |
| ✅ | Look | WPF's look first, then the approved tweaks: chips, rounded periods, dark mode, resizable window, Begin and End on their own lines, newest first, folded periods, the "Next" strip, greyed past dates. Heebo font, messages as pills and sheets. Checked and approved by hand on Windows, macOS and Linux | 2.2.0 to 2.3.2 |
| ✅ | Languages | English and Hebrew, switched in Settings. Hebrew dates in tooltips, a Hebrew month picker, Hebrew or Gregorian dates in the fields | 2.3.0 to 2.3.1 |
| ✅ | Date picker | The app's own month grid for both calendars, dates picked only from it. The weekday row on a band, so its letters aren't taken for dates | 2.3.3, 2.4.2 |
| ✅ | Reminders | Approved wording in English and Hebrew, detailed or discreet. The Reminders window and a calendar file | 2.4.0 |
| ✅ | Reminders | Google Calendar on the desktop, one shared "Purity" calendar for every computer. Checked against the real Google Calendar on Windows, macOS and Linux | 2.4.1 |
| ✅ | Fixes | A crash on clicking a Hebrew text box holding a Mac path or mixed text, fixed with Avalonia 12.1.5 (rechecked on Monterey) | 2.4.2 |
| ✅ | Wipe-out | A red "Remove all history..." button in Settings. One question, with what a synced folder means and that it can't be undone. It removes the history and all its backups, removes Purity's Google calendar and signs out, then closes Purity. The settings stay, apart from where the history was | 2.4.2 |
| ✅ | Welcome | A warmer first-start message that names Dropbox, Google Drive and OneDrive | 2.4.2 |
| ✅ | New period | Starts on the computer's own date, not the UTC one, which in an American evening was already tomorrow and just after midnight in Israel still yesterday. Only the suggestion changed: a picked date was always stored as picked | 2.4.2 |

## Next, on the desktop

- ⬜ Merge `calendar-reminders` into `master`.
- ⬜ Reminders through iCloud and other CalDAV calendars.
- ⬜ Check the app on the latest macOS on Apple Silicon (the MacBook Neo). Monterey is checked on Intel only.
- ⬜ Reminders that follow one shared time zone, so computers in different time zones don't keep moving each other's alerts.

## Calculations: rules version 2

- ⏳ The rav's answers to the open questions on the calculations. Each answer lands as a versioned rules change, with a reviewed list of every date that moves.
- ⬜ An Or Zarua option.
- ✅ A typo in one stored period's end date, fixed by you in the data on 2026-11-11.
- ⬜ Warnings for odd input, such as an end before its start or overlapping periods.
- ⏳ Which onah applies after crossing time zones, a question for the rav.

## Phones

- ⬜ iPhone: the app, the history through the Files picker, then reminders through the phone's calendar and notifications.
- ⬜ Apple Watch: a viewer of what the phone shows.
- ⬜ Android: the app, the history in Dropbox or Google Drive, reminders through the phone's calendar.

## Waiting on you

- ⏳ Publish the Google project. Sign-ins of a project in Testing expire after 7 days.
- ⏳ The rav's answers.
- ⏳ Before the iPhone: the Apple Developer Program, and a Mac that runs current Xcode, which Monterey can't.
- ⏳ Signing and notarizing the Mac app, which needs the Apple Developer Program too.
- ⏳ Before iCloud reminders: an Apple Account with no family calendars, and its app-specific password, entered by you.

## Only if needed

- ⬜ The Dropbox API, if folder sync proves unreliable on the iPhone.

## Dropped

- ✖️ Outlook reminders, dropped on 2026-11-11.
- ✖️ The old WPF and Avalonia 0.10 apps, replaced by this one on 2026-11-09. Their source is kept in history at the tags `wpf-final` and `engine-v1-reference`.

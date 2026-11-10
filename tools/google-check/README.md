# Trying reminders with Google Calendar

This kit tries Purity's reminders against the real Google Calendar, which the automatic tests
can't reach. It holds Purity built with your Google project's client ID and secret, and a made-up
history. It never reads or changes your real data file, and its settings stay inside the kit, so
your installed Purity doesn't see them.

The app in the kit carries your client ID and secret. Keep the kit to yourself, and delete it when
you're done.

## Building it

On Windows, in the repo:

```
dotnet run tools/google-check/build-kit.cs
```

It asks for the client ID and secret of the Desktop client under Google Auth Platform > Clients.
The secret isn't shown as you type or paste it. It builds the kits for Windows, an Intel Mac and
Linux x64 into `out/google-check`, which git ignores. For an Apple Silicon Mac or an ARM Linux
machine, name the kits you need, for example
`dotnet run tools/google-check/build-kit.cs -- windows mac-arm`.

The made-up history's newest period ended two days before the build, so its mikveh reminder is
five days away and the other dates three to four weeks away. Rebuild if more than five days pass
before you try it. Building again starts every kit afresh.

## Before you start

- Use the Google account you added as a test user. A spare account keeps the test out of your own
  calendars.
- On your phone, install Google Calendar, sign in with that account, and allow its notifications.
  The iPhone's own Calendar app may not show a new Google calendar until it's ticked at
  calendar.google.com/calendar/syncselect, so Google's app is the surer test.
- Answer each check with yes or no. If one fails, note what Purity said and take a screenshot.

## On Windows

Open `out/google-check/purity-google-windows`. It has two make-believe computers, each with its own
settings and sign-in: `first computer.cmd` and `second computer.cmd`.

1. **Connecting.** Double-click `first computer.cmd`. Click the gear, then Reminders..., Google
   and Connect Google account.... Your browser opens Google's page. Choose the test account.
   Google warns that it hasn't verified the app: choose Continue. Allow both permissions it asks
   for. The page then says you're signed in.
   - Check: Purity says it's connected.
2. **The calendar.** Choose All dates, press OK, then OK in Settings. Open Google Calendar on the
   web.
   - Check: a calendar named Purity is listed, with entries at the times the Reminders window
     showed under Next reminders.
   - Check: an entry's description ends with a line starting `ref:pr1`.
   - Check: in the Purity calendar's settings, its description starts with `ref:pr1`, followed by
     a short code.
3. **The phone.** Open Reminders again and press Send a test.
   - Check: Test reminder shows on the phone about two minutes later. If your watch is paired,
     note whether it shows there too.
4. **Language.** In Settings, switch to Hebrew and press OK.
   - Check: the same entries now have Hebrew titles, and none were added. Switch back to English.
5. **A new period.** Click New period.
   - Check: the reminders leave the Purity calendar.
   - Remove the new period. Check: they come back.
6. **Remembering.** Close Purity and double-click `first computer.cmd` again.
   - Check: the Reminders window still shows it's connected, without signing in again.
7. **A second computer.** Double-click `second computer.cmd` and connect it to the same account.
   - Check: its Reminders window shows All dates, taken from the calendar.
   - Press OK. Check: Google Calendar still has one Purity calendar, with no doubled entries.
   - Open Reminders in it again, choose Mikveh only and press OK. Check: only the mikveh entry is
     left.
   - In the first computer, click Refresh, then open Reminders. Check: it shows Mikveh only.

## On the Mac, and on Linux if you can

Copy `purity-google-mac-intel.zip`, or the Linux one, to that computer and unzip it. In Terminal,
type `bash` and a space, drag `open-purity.command` from the kit folder in, and press Return.

8. Connect as in step 1, with the same account.
   - Check: it shows Mikveh only, and Google Calendar still has one Purity calendar with no
     doubled entries.
   - Close Purity and run `open-purity.command` again. Check: it's still connected.

On Linux the sign-in is kept with `secret-tool`. If Purity says this computer has nowhere safe to
keep it, install it with `sudo apt install libsecret-tools` and try again.

## Last: removing everything

9. On one computer, open Reminders, press Stop and remove everything..., then Remove.
   - Check: the Purity calendar is gone from Google Calendar.
   - Check: at myaccount.google.com/connections the app no longer has access.
   - Google ends the sign-in on every computer at once, so the others ask you to sign in again
     the next time they update. Press Stop and remove everything on each of them too, which clears
     its saved sign-in.

Then delete the kit folders and zips. On a Mac, Stop and remove everything has already deleted the
sign-in from the keychain.

## What to send back

The check numbers with yes or no, and for each no, what Purity showed. Nothing from the kit needs
sending.

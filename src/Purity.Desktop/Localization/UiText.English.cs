using System.Globalization;


namespace Purity.Desktop.Localization
{
	public sealed partial class UiText
	{
		public static readonly UiText English = new()
		{
			Settings = "Settings",
			Refresh = "Refresh",
			NewPeriod = "New period",
			ClosePeriod = "Close period",
			RemovePeriod = "Remove period",
			FoldPeriod = "Fold this period",
			OpenPeriodFrom = date => $"Open the period from {date}",
			From = "From",
			To = "To",
			FromDayOrNight = "From day or night",
			ToDayOrNight = "To day or night",
			Day = "Day",
			Night = "Night",
			AfterDawn = "After dawn",
			AfterDusk = "After dusk",
			Skip = "Skip",
			Skipped = "Skipped",
			SkipTip = "Omit this period from Veset Aflaga calculation due to some external reasons (surgery, intrauterine device installation, etc)",
			Next = "Next",
			NoPeriodsYet = "No periods yet",
			Press = "Press",
			WhenOneBegins = "when one begins.",

			Mikveh = "Mikveh",
			VesetHodesh = "Veset Hachodesh",
			VesetAflaga = "Veset Aflaga",
			OnaBeinonit = "Ona Benonis",

			Today = "Today",
			Tomorrow = "Tomorrow",
			InDays = days => $"in {days} days",
			TodayLine = date => $"Today, {date}",
			HebrewDateTip = (date, night) => $"{date}, {(night ? "night" : "day")}",
			GregorianNightTip = (first, second) => first.Month == second.Month
				? $"Night of {first.ToString("dddd d", CultureInfo.InvariantCulture)} to {second.ToString("dddd d MMMM", CultureInfo.InvariantCulture)}"
				: $"Night of {first.ToString("dddd d MMMM", CultureInfo.InvariantCulture)} to {second.ToString("dddd d MMMM", CultureInfo.InvariantCulture)}",

			DataFilePathLabel = "Data file path:",
			PickFileTip = "Use a data file you already have, such as the one your other computers use",
			AppearanceLabel = "Appearance:",
			AppearanceName = "Appearance",
			SameAsSystem = "Same as the system",
			Light = "Light",
			Dark = "Dark",
			LanguageLabel = "Language:",
			LanguageName = "Language",
			CalendarLabel = "Calendar:",
			CalendarName = "Calendar",
			GregorianAndHebrew = "Gregorian and Hebrew",
			HebrewOnly = "Hebrew only",
			HebrewInEnglishLabel = "Write Hebrew dates in English letters",
			RestoreEarlier = "Restore an earlier version...",
			RestoreEarlierTip = "Bring back your history as it was before a change. The version it replaces is kept too.",
			MoveFile = "Move the data file to another folder...",
			MoveFileTip = "Copy your history to another folder, such as one Dropbox or OneDrive syncs, and use it from there. The file here stays as it is.",
			CreateReport = "Create periods lengths report",
			RemindersOpen = "Reminders...",
			RemindersTitle = "Reminders",
			RemindersWhere = "Where:",
			RemindersWhereName = "Where reminders go",
			RemindersFile = "File",
			RemindersOther = "Other",
			RemindersLater = "Coming in a later version",
			RemindersAbout = "About what:",
			RemindersAboutName = "What to remind about",
			RemindersMikvehOnly = "Mikveh only",
			RemindersAllDates = "All dates",
			RemindersChosen = "Choose",
			RemindersWording = "Wording:",
			RemindersWordingName = "How reminders are worded",
			RemindersDiscreet = "Discreet",
			RemindersDetailed = "Detailed",
			RemindersNext = "Next reminders",
			RemindersNone = "There are no reminders to come.",
			RemindersOpenPeriod = "A period is open. Its reminders appear once it's closed.",
			RemindersMore = "More",
			RemindersMikvehTime = "Mikveh alert:",
			RemindersOtherTime = "Other alerts:",
			RemindersMoveOffShabbat = "Move alerts off Shabbat to Friday at 08:00",
			RemindersCalendarName = "Calendar name:",
			RemindersNotice = "The file is a one-time copy. If your dates change, save it again. Importing it stores these dates with your calendar service.",
			RemindersSave = "Save reminders file...",
			RemindersFileTitle = "Reminders file",
			RemindersFileType = "Calendar file",
			RemindersSaved = "The reminders file is saved. Open it to add the reminders to your calendar.",
			RemindersFileFailed = "The reminders file couldn't be written.",
			RemindersNotSaved = "The reminder choices couldn't be saved, so Purity will start with the earlier ones next time.\n\n",
			RemindersGoogleTip = "Your Google calendar",
			RemindersGoogleNotInBuild = "Google isn't set up in this build",
			RemindersGoogleIntro = "Purity makes its own calendar in your Google account. It can see the names of your other calendars, but can't read or change them. These dates will be stored with Google.",
			RemindersConnect = "Connect Google account...",
			RemindersConnecting = "Waiting for you to sign in, in your browser...",
			RemindersConnected = "Connected. Purity updates this calendar whenever your dates change.",
			RemindersSendTest = "Send a test",
			RemindersTestSent = "A test reminder will arrive in about two minutes.",
			RemindersDisconnect = "Stop and remove everything...",
			RemindersDisconnectQuestion = "Remove Purity's calendar from your Google account?",
			RemindersDisconnectBody = "This deletes the calendar and all its reminders, and Purity forgets the sign-in.",
			RemindersDisconnectConfirm = "Remove",
			RemindersSignInDone = "You're signed in. You can close this tab and go back to Purity.",
			RemindersNoConnection = "Purity couldn't reach Google. It will try again the next time your dates change.",
			RemindersSignInNeeded = "Sign in to Google again to keep your reminders up to date.",
			RemindersSignInNotFinished = "The Google sign-in wasn't finished.",
			RemindersCalendarFailed = "Google didn't accept the update. Purity will try again the next time your dates change.",
			RemindersNotRemembered = "This computer has nowhere safe to keep the sign-in, so you'll need to connect again next time.",
			Ok = "OK",
			Cancel = "Cancel",
			ReportTitle = "Periods lengths report",
			ReportFailed = "The report couldn't be written.",
			DataFileTitle = "Data file",
			MoveToTitle = "Move the data file to",
			DataFileType = "Purity data",

			EarlierVersions = "Earlier versions",
			RestoreEllipsis = "Restore...",
			NoBackupsYet = "This computer has no earlier versions yet. One is kept each time your history changes.",
			ChooseBackup = "Each time your history changed, this computer kept the version it replaced. Choose the one to bring back:",

			Yes = "Yes",
			No = "No",
			Close = "Close",
			KeepOpen = "Keep open",
			DuplicateToday = "A period beginning today is already in the history, and two periods can't begin at the same time.\n\nTo add another, first change that period's start date.",
			EndNotYet = end => $"This period's end date, {end}, hasn't come yet.",
			ClosePeriodAnyway = "Close the period anyway?",
			CloseAnyway = "Close anyway?",
			RemoveQuestion = "Are you sure you want to remove this period?",

			InCopy = name => $"In the copy \"{name}\"",
			KeepCopy = "Keep the copy's",
			InBackup = when => $"In the backup from {when}",
			KeepBackup = "Keep the backup's",
			InDataFile = "In the data file",
			KeepFile = "Keep the file's",
			KeepThisComputer = "Keep this computer's",
			ConflictIntro = "Your history was changed in two places, and the two versions disagree here.\n\nOn this computer:\n",
			WhichVersion = "Which version do you want to keep?",
			Nothing = "nothing",
			PeriodRange = (begin, end) => $"{begin} to {end}",
			OpenSuffix = ", open",
			SkippedSuffix = ", skipped",
			NightSuffix = " (night)",
			DaySuffix = " (day)",

			WarnSelfTest = "Purity's check of its date calculations failed on this computer, so it shows no dates. Don't rely on this copy of the app.",
			WarnChooseFile = "Choose your data file in Settings.",
			WarnUnreadable = "The data file can't be read. Nothing is written to it until it can be; your entries are kept on this computer.",
			WarnTwoVersions = "Two versions of your history disagree. Press Refresh to choose which to keep. Nothing is saved until then.",
			WarnNotWritten = "The data file couldn't be written. Your entries are kept on this computer and saved on the next change.",
			WarnCopies = count => count == 1
				? "A copy of the data file left by your sync app is beside it and not brought in yet. Press Refresh to bring it in."
				: $"{count} copies of the data file left by your sync app are beside it and not brought in yet. Press Refresh to bring them in.",
			WarnVault = "This computer's backup copy couldn't be written or read. Saving to the data file still works.",

			Unreadable =
				"The data file couldn't be read, so you're seeing the copy this computer kept last time, if it has one. Recent entries may be missing.\n\n" +
				"Nothing will be written to the data file until it can be read. Anything you enter now is kept on this computer and added once it can be read, for example when your sync finishes.",
			SelfTestFailed =
				"Purity checks its date calculations every time it starts, and on this computer they came out different from the approved ones. " +
				"So it shows no dates, and it hasn't opened your data file, so nothing in it can change.\n\n" +
				"Please don't rely on this copy of the app, and let the developer know what the check found:\n\n",
			UnsavedOnClose = "Some entries couldn't be saved to the data file yet.\n\nThey're kept on this computer, and Purity saves them the next time it opens.",

			BringThemIn = "Bring them in",
			NotNow = "Not now",
			CopyUnreadable = name => $"\"{name}\" can't be read, so it was left where it is.",
			CopyNotWritten = name => $"\"{name}\" was left where it is, because the data file couldn't be written.",
			CopyDisagrees = name => $"\"{name}\" disagrees with your history about a period. Press Refresh when you're ready to choose.",
			CopyLeft = name => $"\"{name}\" was left where it is.",
			CopyBroughtIn = name => $"\"{name}\" is brought in and now in this computer's backups.",
			CopyBackupFailed = name => $"The entries of \"{name}\" are in your history, but the copy stays beside the data file, because this computer's backups couldn't be written.",
			CopyChanged = name => $"\"{name}\" changed while it was being brought in, so it was left where it is.",
			CopyNotRemoved = name => $"The entries of \"{name}\" are in your history, but the copy couldn't be removed from beside the data file.",
			AndMore = count => $"and {count} more",
			CopiesQuestion = (count, names) =>
				(count == 1 ? "Your sync app left a copy" : $"Your sync app left {count} copies") +
				" of the data file beside it, from a time when two devices saved at once:\n\n" + names + "\n\n" +
				(count == 1 ? "It may hold entries that are missing here." : "They may hold entries that are missing here.") +
				"\n\nNothing in your history is removed. If a copy disagrees about a period, you choose which version to keep. " +
				"Afterwards each copy moves to this computer's backups.",
			BringInTitle = count => count == 1 ? "Bring it in?" : "Bring them in?",
			CopiesReport = (setAside, added) =>
			{
				var copies = setAside == 1 ? "1 copy" : $"{setAside} copies";
				var periods = added switch
				{
					0 => setAside == 1 ? "It held nothing new" : "They held nothing new",
					1 => "1 new period came from " + (setAside == 1 ? "it" : "them"),
					_ => $"{added} new periods came from " + (setAside == 1 ? "it" : "them"),
				};
				return $"Brought in {copies}. {periods}, and {(setAside == 1 ? "it is" : "they are")} now in this computer's backups.";
			},

			FirstStartTitle = "Purity keeps your history in one data file.",
			FirstStart =
				"Start a new history on this computer, or open the data file you already have, such as the one your other computers use in Dropbox. " +
				"A new history can move to a synced folder later, in Settings.",
			StartNew = "Start a new history",
			OpenMine = "Open my data file...",
			SettingsNotSaved = "The settings couldn't be saved, so Purity will ask for the data file again next time.\n\n",
			MoveOntoFile =
				"There's already a file with that name there, and Purity won't replace it, since it may hold someone's history.\n\n" +
				"To use that file instead, choose it with the ... button in Settings.",
			CantMoveYet = "The history can't move yet, because some entries haven't reached the data file. Try again once they have.",
			CopyFailed = "The history couldn't be copied there, so it stays where it is.\n\n",
			Moved = (to, from) => $"Your history is now kept in {to}.\n\nThe old file is still in {from}, unchanged, and Purity no longer uses it.",

			RestoreConfirm = when => $"Bring back your history as it was until {when}?",
			RestoreConfirmBody =
				"The history as it is now is kept as a backup too, so you can go back to it the same way. " +
				"Entries that haven't reached the data file yet are kept.",
			Restore = "Restore",
			BackupUnreadable = "That backup can't be read, so nothing was changed.",
			RestoreNotWritten = "The data file couldn't be written, so the restored version is kept on this computer and saved on the next change.",
			Restored = when => $"Your history is back as it was until {when}. The version from just before is now a backup too.",
			Until = when => $"Until {when}: ",
			NoPeriods = "no periods",
			PeriodCount = count => count == 1 ? "1 period" : $"{count} periods",
			LastBeginning = date => $", the last beginning {date}",
			SameAsNow = ", the same as now",
			CantBeRead = "can't be read",

			AppearanceNotSaved = "The appearance couldn't be saved, so Purity will start with the earlier one next time.\n\n",
			LanguageNotSaved = "The language couldn't be saved, so Purity will start with the earlier one next time.\n\n",
			CalendarNotSaved = "The calendar couldn't be saved, so Purity will start with the earlier one next time.\n\n",
		};
	}
}

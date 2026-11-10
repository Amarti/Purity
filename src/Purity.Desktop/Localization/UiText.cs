using Purity.Core;


namespace Purity.Desktop.Localization
{
	/// <summary>
	/// Every word the app shows. Each language fills in all of them, in UiText.English.cs and
	/// UiText.Hebrew.cs, and a text left out of a language doesn't build. Texts with numbers are
	/// functions, because languages form their plurals differently.
	/// </summary>
	public sealed partial class UiText
	{
		/// <summary>The name shown for a kind of calculated date</summary>
		public string EventType(PurityEventType type) => type switch
		{
			PurityEventType.Mikveh => Mikveh,
			PurityEventType.VesetHodesh => VesetHodesh,
			PurityEventType.VesetAflaga => VesetAflaga,
			PurityEventType.OnaBeinonit => OnaBeinonit,
			_ => string.Empty,
		};


		// The main window
		public required string Settings { get; init; }
		public required string Refresh { get; init; }
		public required string NewPeriod { get; init; }
		public required string ClosePeriod { get; init; }
		public required string RemovePeriod { get; init; }
		public required string FoldPeriod { get; init; }
		public required Func<string, string> OpenPeriodFrom { get; init; }
		public required string From { get; init; }
		public required string To { get; init; }
		public required string FromDayOrNight { get; init; }
		public required string ToDayOrNight { get; init; }
		public required string Day { get; init; }
		public required string Night { get; init; }
		public required string AfterDawn { get; init; }
		public required string AfterDusk { get; init; }
		public required string Skip { get; init; }
		public required string Skipped { get; init; }
		public required string SkipTip { get; init; }
		public required string Next { get; init; }
		public required string NoPeriodsYet { get; init; }
		public required string Press { get; init; }
		public required string WhenOneBegins { get; init; }

		// The kinds of date
		public required string Mikveh { get; init; }
		public required string VesetHodesh { get; init; }
		public required string VesetAflaga { get; init; }
		public required string OnaBeinonit { get; init; }

		// How far off a date is
		public required string Today { get; init; }
		public required string Tomorrow { get; init; }
		public required Func<int, string> InDays { get; init; }
		public required Func<string, string> TodayLine { get; init; }

		// A date in the other calendar
		public required Func<string, bool, string> HebrewDateTip { get; init; }
		public required Func<DateTime, DateTime, string> GregorianNightTip { get; init; }

		// The settings window
		public required string DataFilePathLabel { get; init; }
		public required string PickFileTip { get; init; }
		public required string AppearanceLabel { get; init; }
		public required string AppearanceName { get; init; }
		public required string SameAsSystem { get; init; }
		public required string Light { get; init; }
		public required string Dark { get; init; }
		public required string LanguageLabel { get; init; }
		public required string LanguageName { get; init; }
		public required string CalendarLabel { get; init; }
		public required string CalendarName { get; init; }
		public required string GregorianAndHebrew { get; init; }
		public required string HebrewOnly { get; init; }
		public required string HebrewInEnglishLabel { get; init; }
		public required string RestoreEarlier { get; init; }
		public required string RestoreEarlierTip { get; init; }
		public required string MoveFile { get; init; }
		public required string MoveFileTip { get; init; }
		public required string CreateReport { get; init; }
		public required string RemindersOpen { get; init; }
		public required string RemindersTitle { get; init; }
		public required string RemindersWhere { get; init; }
		public required string RemindersWhereName { get; init; }
		public required string RemindersFile { get; init; }
		public required string RemindersOther { get; init; }
		public required string RemindersLater { get; init; }
		public required string RemindersAbout { get; init; }
		public required string RemindersAboutName { get; init; }
		public required string RemindersMikvehOnly { get; init; }
		public required string RemindersAllDates { get; init; }
		public required string RemindersChosen { get; init; }
		public required string RemindersWording { get; init; }
		public required string RemindersWordingName { get; init; }
		public required string RemindersDiscreet { get; init; }
		public required string RemindersDetailed { get; init; }
		public required string RemindersNext { get; init; }
		public required string RemindersNone { get; init; }
		public required string RemindersOpenPeriod { get; init; }
		public required string RemindersMore { get; init; }
		public required string RemindersMikvehTime { get; init; }
		public required string RemindersOtherTime { get; init; }
		public required string RemindersMoveOffShabbat { get; init; }
		public required string RemindersCalendarName { get; init; }
		public required string RemindersNotice { get; init; }
		public required string RemindersSave { get; init; }
		public required string RemindersFileTitle { get; init; }
		public required string RemindersFileType { get; init; }
		public required string RemindersSaved { get; init; }
		public required string RemindersFileFailed { get; init; }
		public required string RemindersNotSaved { get; init; }
		public required string RemindersGoogleTip { get; init; }
		public required string RemindersGoogleNotInBuild { get; init; }
		public required string RemindersGoogleIntro { get; init; }
		public required string RemindersConnect { get; init; }
		public required string RemindersConnecting { get; init; }
		public required string RemindersConnected { get; init; }
		public required string RemindersSendTest { get; init; }
		public required string RemindersTestSent { get; init; }
		public required string RemindersDisconnect { get; init; }
		public required string RemindersDisconnectQuestion { get; init; }
		public required string RemindersDisconnectBody { get; init; }
		public required string RemindersDisconnectConfirm { get; init; }
		public required string RemindersSignInDone { get; init; }
		public required string RemindersNoConnection { get; init; }
		public required string RemindersSignInNeeded { get; init; }
		public required string RemindersSignInNotFinished { get; init; }
		public required string RemindersCalendarFailed { get; init; }
		public required string RemindersNotRemembered { get; init; }
		public required string Ok { get; init; }
		public required string Cancel { get; init; }
		public required string ReportTitle { get; init; }
		public required string ReportFailed { get; init; }
		public required string DataFileTitle { get; init; }
		public required string MoveToTitle { get; init; }
		public required string DataFileType { get; init; }

		// The earlier versions window
		public required string EarlierVersions { get; init; }
		public required string RestoreEllipsis { get; init; }
		public required string NoBackupsYet { get; init; }
		public required string ChooseBackup { get; init; }

		// Questions about periods
		public required string Yes { get; init; }
		public required string No { get; init; }
		public required string Close { get; init; }
		public required string KeepOpen { get; init; }
		public required string DuplicateToday { get; init; }
		public required Func<string, string> EndNotYet { get; init; }
		public required string ClosePeriodAnyway { get; init; }
		public required string CloseAnyway { get; init; }
		public required string RemoveQuestion { get; init; }

		// Two versions of the history
		public required Func<string, string> InCopy { get; init; }
		public required string KeepCopy { get; init; }
		public required Func<string, string> InBackup { get; init; }
		public required string KeepBackup { get; init; }
		public required string InDataFile { get; init; }
		public required string KeepFile { get; init; }
		public required string KeepThisComputer { get; init; }
		public required string ConflictIntro { get; init; }
		public required string WhichVersion { get; init; }
		public required string Nothing { get; init; }
		public required Func<string, string, string> PeriodRange { get; init; }
		public required string OpenSuffix { get; init; }
		public required string SkippedSuffix { get; init; }
		public required string NightSuffix { get; init; }
		public required string DaySuffix { get; init; }

		// The line under the list
		public required string WarnSelfTest { get; init; }
		public required string WarnChooseFile { get; init; }
		public required string WarnUnreadable { get; init; }
		public required string WarnTwoVersions { get; init; }
		public required string WarnNotWritten { get; init; }
		public required Func<int, string> WarnCopies { get; init; }
		public required string WarnVault { get; init; }

		// Messages about the data file
		public required string Unreadable { get; init; }
		public required string SelfTestFailed { get; init; }
		public required string UnsavedOnClose { get; init; }

		// Copies left by a sync app
		public required string BringThemIn { get; init; }
		public required string NotNow { get; init; }
		public required Func<string, string> CopyUnreadable { get; init; }
		public required Func<string, string> CopyNotWritten { get; init; }
		public required Func<string, string> CopyDisagrees { get; init; }
		public required Func<string, string> CopyLeft { get; init; }
		public required Func<string, string> CopyBroughtIn { get; init; }
		public required Func<string, string> CopyBackupFailed { get; init; }
		public required Func<string, string> CopyChanged { get; init; }
		public required Func<string, string> CopyNotRemoved { get; init; }
		public required Func<int, string> AndMore { get; init; }
		public required Func<int, string, string> CopiesQuestion { get; init; }
		public required Func<int, string> BringInTitle { get; init; }
		public required Func<int, int, string> CopiesReport { get; init; }

		// Where the history is kept
		public required string FirstStart { get; init; }
		public required string FirstStartTitle { get; init; }
		public required string StartNew { get; init; }
		public required string OpenMine { get; init; }
		public required string SettingsNotSaved { get; init; }
		public required string MoveOntoFile { get; init; }
		public required string CantMoveYet { get; init; }
		public required string CopyFailed { get; init; }
		public required Func<string, string, string> Moved { get; init; }

		// Earlier versions of the history
		public required Func<string, string> RestoreConfirm { get; init; }
		public required string RestoreConfirmBody { get; init; }
		public required string Restore { get; init; }
		public required string BackupUnreadable { get; init; }
		public required string RestoreNotWritten { get; init; }
		public required Func<string, string> Restored { get; init; }
		public required Func<string, string> Until { get; init; }
		public required string NoPeriods { get; init; }
		public required Func<int, string> PeriodCount { get; init; }
		public required Func<string, string> LastBeginning { get; init; }
		public required string SameAsNow { get; init; }
		public required string CantBeRead { get; init; }

		// Settings that couldn't be kept
		public required string AppearanceNotSaved { get; init; }
		public required string LanguageNotSaved { get; init; }
		public required string CalendarNotSaved { get; init; }
	}
}

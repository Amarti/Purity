using CommunityToolkit.Mvvm.ComponentModel;
using Purity.Desktop.Localization;


namespace Purity.Desktop.ViewModels
{
	public sealed partial class SettingsWindowViewModel : ObservableObject
	{
		/// <param name="dataFilePath">The data file in use</param>
		/// <param name="report">Builds the periods lengths report, or null when there is nothing to report</param>
		/// <param name="canRestore">Whether this computer has earlier versions of the history to bring back</param>
		/// <param name="canMove">Whether the history can move to another folder now</param>
		/// <param name="appearance">Light, dark, or the system's choice, as set now</param>
		/// <param name="language">The language being shown</param>
		/// <param name="showLanguage">Shows the whole app in a language, the moment it's chosen</param>
		/// <param name="calendar">The calendar dates are picked and shown in</param>
		/// <param name="hebrewInEnglish">Whether Hebrew dates are written in English letters</param>
		/// <param name="showCalendar">Shows the whole app with a calendar and letters, the moment they're chosen</param>
		public SettingsWindowViewModel(string dataFilePath, Func<string?>? report, bool canRestore = false, bool canMove = false, Appearance appearance = Appearance.System, Language language = Language.English, Action<Language>? showLanguage = null, CalendarKind calendar = CalendarKind.Gregorian, bool hebrewInEnglish = false, Action<CalendarKind, bool>? showCalendar = null)
		{
			DataFilePath = dataFilePath;
			_report = report;
			CanRestore = canRestore;
			CanMove = canMove;
			Appearance = appearance;
			Language = language;
			_showLanguage = showLanguage;
			CalendarIndex = (int)calendar;
			HebrewInEnglish = hebrewInEnglish;
			_showCalendar = showCalendar;
		}


		public Appearance Appearance { get; private set; }

		public bool IsSameAsSystem
		{
			get => Appearance == Appearance.System;
			set
			{
				if (value)
					Choose(Appearance.System);
			}
		}

		public bool IsLight
		{
			get => Appearance == Appearance.Light;
			set
			{
				if (value)
					Choose(Appearance.Light);
			}
		}

		public bool IsDark
		{
			get => Appearance == Appearance.Dark;
			set
			{
				if (value)
					Choose(Appearance.Dark);
			}
		}

		/// <summary>The choices for <see cref="Calendar"/>, in the order of its values</summary>
		public IReadOnlyList<string> CalendarChoices => new[] { Loc.T.GregorianAndHebrew, Loc.T.HebrewOnly };

		/// <summary>Which of <see cref="CalendarChoices"/> is picked. Shows at once, like the language.</summary>
		[ObservableProperty]
		public partial int CalendarIndex { get; set; }

		public CalendarKind Calendar => CalendarIndex == (int)CalendarKind.Hebrew ? CalendarKind.Hebrew : CalendarKind.Gregorian;

		/// <summary>Whether Hebrew dates are written in English letters. Shows at once, like the language.</summary>
		[ObservableProperty]
		public partial bool HebrewInEnglish { get; set; }

		partial void OnCalendarIndexChanged(int value)
		{
			if (value >= 0)
				_showCalendar?.Invoke(Calendar, HebrewInEnglish);
		}

		partial void OnHebrewInEnglishChanged(bool value) => _showCalendar?.Invoke(Calendar, value);

		/// <summary>The language chosen so far</summary>
		public Language Language { get; private set; }

		public bool IsEnglish
		{
			get => Language == Language.English;
			set
			{
				if (value)
					Choose(Language.English);
			}
		}

		public bool IsHebrew
		{
			get => Language == Language.Hebrew;
			set
			{
				if (value)
					Choose(Language.Hebrew);
			}
		}


		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanAccept))]
		public partial string DataFilePath { get; set; }

		/// <summary>OK needs a data file to use</summary>
		public bool CanAccept => !string.IsNullOrWhiteSpace(DataFilePath);

		public bool CanReport => _report != null;

		public bool CanRestore { get; }

		public bool CanMove { get; }

		/// <summary>The person asked to bring back an earlier version, which closes the settings without changing them</summary>
		public bool RestoreRequested { get; private set; }

		public void RequestRestore() => RestoreRequested = true;

		/// <summary>Where the person asked to move the history, which closes the settings without changing them</summary>
		public string? MoveTo { get; private set; }

		public void RequestMove(string path) => MoveTo = path;

		/// <inheritdoc cref="SettingsWindowViewModel(string, Func{string?}?, bool, bool, Appearance, Language, Action{Language}?, CalendarKind, bool, Action{CalendarKind, bool}?)"/>
		public string? BuildReport() => _report?.Invoke();


		private void Choose(Appearance appearance)
		{
			Appearance = appearance;
			OnPropertyChanged(nameof(IsSameAsSystem));
			OnPropertyChanged(nameof(IsLight));
			OnPropertyChanged(nameof(IsDark));
		}

		/// <summary>The app changes language at once, this window included. The calendar list has to be filled again with the new words.</summary>
		private void Choose(Language language)
		{
			if (language == Language)
				return;

			Language = language;
			OnPropertyChanged(nameof(IsEnglish));
			OnPropertyChanged(nameof(IsHebrew));
			_showLanguage?.Invoke(language);

			var calendar = CalendarIndex;
			CalendarIndex = -1;
			OnPropertyChanged(nameof(CalendarChoices));
			CalendarIndex = calendar;
		}


		private readonly Func<string?>? _report;
		private readonly Action<Language>? _showLanguage;
		private readonly Action<CalendarKind, bool>? _showCalendar;
	}
}

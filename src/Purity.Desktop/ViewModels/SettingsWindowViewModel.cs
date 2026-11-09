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
		public SettingsWindowViewModel(string dataFilePath, Func<string?>? report, bool canRestore = false, bool canMove = false, Appearance appearance = Appearance.System, Language language = Language.English, Action<Language>? showLanguage = null)
		{
			DataFilePath = dataFilePath;
			_report = report;
			CanRestore = canRestore;
			CanMove = canMove;
			AppearanceIndex = (int)appearance;
			Language = language;
			_showLanguage = showLanguage;
		}


		/// <summary>The choices for <see cref="Appearance"/>, in the order of its values</summary>
		public IReadOnlyList<string> AppearanceChoices => new[] { Loc.T.SameAsSystem, Loc.T.Light, Loc.T.Dark };

		/// <summary>Which of <see cref="AppearanceChoices"/> is picked</summary>
		[ObservableProperty]
		public partial int AppearanceIndex { get; set; }

		public Appearance Appearance => Enum.IsDefined((Appearance)AppearanceIndex) ? (Appearance)AppearanceIndex : Appearance.System;

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

		/// <inheritdoc cref="SettingsWindowViewModel(string, Func{string?}?, bool, bool, Appearance, Language, Action{Language}?)"/>
		public string? BuildReport() => _report?.Invoke();


		/// <summary>The app changes language at once, this window included. The appearance list has to be filled again with the new words.</summary>
		private void Choose(Language language)
		{
			if (language == Language)
				return;

			Language = language;
			OnPropertyChanged(nameof(IsEnglish));
			OnPropertyChanged(nameof(IsHebrew));
			_showLanguage?.Invoke(language);

			var appearance = AppearanceIndex;
			AppearanceIndex = -1;
			OnPropertyChanged(nameof(AppearanceChoices));
			AppearanceIndex = appearance;
		}


		private readonly Func<string?>? _report;
		private readonly Action<Language>? _showLanguage;
	}
}

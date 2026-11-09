using System.Collections.ObjectModel;
using Purity.Desktop.Localization;
using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>How the app looks on this computer: light or dark (T7), where the window was (T8), and its language</summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>Light, dark, or the system's choice, as set in Settings</summary>
		public Appearance Appearance => _settings.Appearance;

		/// <summary>Where the main window was when it last closed, or null</summary>
		public WindowPlacement? WindowPlacement => _settings.Window;

		/// <summary>
		/// Keeps the main window's place for next time. Only with a data file open, whether from the
		/// settings or for this run only, so closing the first question still leaves no settings
		/// behind. Not being able to save it only means the window opens where it did before.
		/// </summary>
		public void RememberWindow(WindowPlacement placement)
		{
			if (string.IsNullOrWhiteSpace(DataFilePath) || placement == _settings.Window)
				return;

			_settings.Window = placement;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// the window opens where it did before
			}
		}


		/// <summary>
		/// Shows the whole app in this language at once. Saves nothing: that waits for OK. The date
		/// fields and the calendars behind them write their month names when they're built, so the
		/// periods are built again.
		/// </summary>
		private void ShowLanguage(Language language)
		{
			if (language == Loc.Instance.Language)
				return;

			Loc.Instance.Use(language);
			OnPropertyChanged(nameof(AddOrCloseLabel));
			UpdateWarning();
			RefreshAll();
			BuildAgain(OpenPeriods);
			BuildAgain(ClosedPeriods);
		}

		private static void BuildAgain(ObservableCollection<PeriodViewModel> shown)
		{
			var periods = shown.ToList();
			shown.Clear();
			foreach (var item in periods)
				shown.Add(item);
		}

		private async Task SetLanguageAsync(Language language)
		{
			if (language == _settings.Language)
				return;

			_settings.Language = language;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				await _dialogs.AskAsync(T.LanguageNotSaved + e.Message, T.Ok);
			}
		}


		private async Task SetAppearanceAsync(Appearance appearance)
		{
			if (appearance == _settings.Appearance)
				return;

			_settings.Appearance = appearance;
			OnPropertyChanged(nameof(Appearance));
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				await _dialogs.AskAsync(T.AppearanceNotSaved + e.Message, T.Ok);
			}
		}
	}
}

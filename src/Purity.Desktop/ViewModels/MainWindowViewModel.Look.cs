namespace Purity.Desktop.ViewModels
{
	/// <summary>How the app looks on this computer: light or dark (T7), and where the window was (T8)</summary>
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
				await _dialogs.AskAsync("The appearance couldn't be saved, so Purity will start with the earlier one next time.\n\n" + e.Message, "OK");
			}
		}
	}
}

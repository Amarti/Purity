using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>Setting up reminders from the settings window</summary>
	public sealed partial class MainWindowViewModel
	{
		private RemindersWindowViewModel NewReminders() => new(_settings.Reminders, Ledger.Data, _clock, SaveReminders);

		private void SaveReminders(ReminderSetup setup)
		{
			_settings.Reminders = setup;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_dialogs.Tell(T.RemindersNotSaved + e.Message, MessageKind.Problem);
			}
		}
	}
}

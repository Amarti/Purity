using Purity.Storage;


namespace Purity.Desktop.ViewModels
{
	/// <summary>What the view models need from windows, so they can be tested without any</summary>
	public interface IAppDialogs
	{
		/// <summary>Shows a message and waits for one of the buttons</summary>
		/// <returns>The index of the button pressed, or null when the window was closed without one</returns>
		Task<int?> AskAsync(string text, params string[] buttons);

		/// <summary>Opens the settings window</summary>
		/// <returns>Whether the person pressed OK</returns>
		Task<bool> ShowSettingsAsync(SettingsWindowViewModel settings);

		/// <summary>Lists the earlier versions of the history and lets the person pick one</summary>
		/// <returns>The version picked, or null</returns>
		Task<Backup?> ChooseBackupAsync(IReadOnlyList<BackupChoice> choices);

		/// <summary>Lets the person pick a data file they already have</summary>
		/// <returns>Its path, or null</returns>
		Task<string?> PickDataFileAsync();
	}


	/// <param name="Backup">The earlier version</param>
	/// <param name="Label">How it's shown: when it was replaced and what it holds</param>
	public sealed record BackupChoice(Backup Backup, string Label);
}

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
	}
}

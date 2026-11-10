using Purity.Storage;


namespace Purity.Desktop.ViewModels
{
	/// <summary>What the view models need from windows, so they can be tested without any</summary>
	public interface IAppDialogs
	{
		/// <summary>Says something that needs no answer, and carries on at once</summary>
		void Tell(string text, MessageKind kind);

		/// <summary>Asks a question and waits for one of the buttons</summary>
		/// <param name="question">What's being asked</param>
		/// <param name="buttons">The answers. The first is the one that goes ahead, unless the question has no preference.</param>
		/// <returns>The index of the button pressed, or null when the question was closed without one</returns>
		Task<int?> AskAsync(Question question, params string[] buttons);

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


	/// <summary>What kind of thing is being said</summary>
	public enum MessageKind
	{
		/// <summary>Something went well. It fades by itself.</summary>
		Done,

		/// <summary>Something went wrong. It stays until it's closed.</summary>
		Problem,

		/// <summary>Something that makes the app unsafe to rely on for now. It stays until it's closed.</summary>
		Critical,
	}


	/// <param name="Title">The question itself</param>
	/// <param name="Body">What's needed to answer it, or null</param>
	/// <param name="NoPreference">Neither answer is the safer one, so no button stands out</param>
	public sealed record Question(string Title, string? Body = null, bool NoPreference = false);


	/// <param name="Backup">The earlier version</param>
	/// <param name="Label">How it's shown: when it was replaced and what it holds</param>
	public sealed record BackupChoice(Backup Backup, string Label);
}

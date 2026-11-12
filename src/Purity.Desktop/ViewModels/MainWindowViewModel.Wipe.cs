using System.Net.Http;
using Purity.Reminders;
using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>
	/// Removing the history and all its backups from this computer, for good: the data file in use with
	/// any copies a sync app left beside it, a history started here earlier, and every backup and local
	/// copy in app data. When there's a calendar sign-in, Purity's calendar goes too, since its reminders
	/// come from the history, and the sign-in ends. The settings stay, as they hold nothing private, but
	/// forget where the history was, so the next start welcomes the person afresh instead of making a new
	/// file in its place. One question, with the sync folders' caveat, then the app closes, so nothing
	/// still running can write the history back.
	/// </summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>Whether there's a history on this computer to remove</summary>
		private bool CanWipe => !string.IsNullOrWhiteSpace(DataFilePath);

		private async Task WipeOutAsync()
		{
			var question = new Question(T.WipeTitle, T.WipeBody, RemovesForGood: true, Warning: T.WipeSure);
			if (await _dialogs.AskAsync(question, T.WipeForGood, T.Cancel) != 0)
				return;

			var dataFile = string.IsNullOrWhiteSpace(DataFilePath) ? null : Path.GetFullPath(DataFilePath);
			var copies = ConflictedCopies();
			var earlier = dataFile == null || !SamePath(AppPaths.DefaultDataFile, dataFile) ? AppPaths.DefaultDataFile : null;

			_reminderCancel.Cancel();
			var left = new List<string>();
			foreach (var (place, connection) in _reminderConnections?.All ?? Enumerable.Empty<KeyValuePair<ReminderPlace, IReminderConnection>>())
				if (connection is { IsAvailable: true, IsConnected: true })
					await RemoveCalendarAsync(connection, place, left);

			CloseDataFile();							// stops watching the file and leaves nothing to save
			_dataFileOverride = null;
			ForgetWhereTheHistoryWas(left);
			foreach (var file in new[] { dataFile, earlier })
			{
				if (file == null)
					continue;
				Remove(file, left);
				foreach (var leftover in Leftovers(file))
					Remove(leftover, left);
			}
			foreach (var copy in copies)
				Remove(copy, left);
			RemoveFolder(AppPaths.VaultsFolder, left);
			foreach (var file in AppPaths.SignInFiles)		// signed out already; this catches one a build without Google left behind
			{
				Remove(file, left);
				Remove(file + ".tmp", left);
			}

			var done = left.Count == 0 ? new Question(T.WipedTitle, T.Wiped) : new Question(T.WipeLeftTitle, T.WipeLeft(Bullets(left)));
			await _dialogs.AskAsync(done, T.ClosePurity);
			_dialogs.CloseApp();
		}


		/// <summary>The calendar holds the dates too. Without a connection it stays, but the sign-in is forgotten all the same.</summary>
		private static async Task RemoveCalendarAsync(IReminderConnection connection, ReminderPlace place, List<string> left)
		{
			try
			{
				await connection.DisconnectAsync(CancellationToken.None);
				return;
			}
			catch (Exception e) when (e is CalendarServiceException or HttpRequestException or OperationCanceledException)
			{
				left.Add(place switch
				{
					ReminderPlace.ICloud => T.WipeICloudCalendarLeft,
					ReminderPlace.CalDav => T.WipeCalDavCalendarLeft,
					_ => T.WipeCalendarLeft,
				});
			}

			try
			{
				await connection.ForgetAsync(CancellationToken.None);
			}
			catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
			{
				// forgotten here already; only telling the service failed
			}
		}

		/// <summary>The rest of the settings stay as they are</summary>
		private void ForgetWhereTheHistoryWas(List<string> left)
		{
			_settings.DataFilePath = string.Empty;
			_settings.GoogleCalendarId = null;
			_settings.ICloudCalendar = null;
			_settings.CalDavCalendar = null;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				left.Add(Isolated(AppPaths.SettingsFile));
			}
		}

		private List<string> ConflictedCopies()
		{
			try
			{
				return _store?.FindConflictedCopies().ToList() ?? new List<string>();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return new List<string>();
			}
		}

		/// <summary>Half-written saves that a crash can leave beside a data file</summary>
		private static IEnumerable<string> Leftovers(string dataFile)
		{
			try
			{
				return Directory.GetFiles(Path.GetDirectoryName(dataFile)!, $"~{Path.GetFileName(dataFile)}.*.tmp");
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return Array.Empty<string>();
			}
		}

		private static void Remove(string file, List<string> left)
		{
			try
			{
				File.Delete(file);					// deleting a file that isn't there is no error
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				left.Add(Isolated(file));
			}
		}

		private static void RemoveFolder(string folder, List<string> left)
		{
			try
			{
				if (Directory.Exists(folder))
					Directory.Delete(folder, recursive: true);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				left.Add(Isolated(folder));
			}
		}

		/// <summary>Keeps a path reading left to right inside Hebrew text, so a Mac path's leading slash stays at its start</summary>
		private static string Isolated(string path) => "⁦" + path + "⁩";

		private static string Bullets(IEnumerable<string> items) => string.Join("\n", items.Select(el => "• " + el));
	}
}

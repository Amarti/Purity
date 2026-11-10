using Purity.Storage;
using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>Where the history is kept: the first start's choice, switching files, and moving it to a synced folder</summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>
		/// On a computer with no data file yet: a new history in this computer's app data folder, or a
		/// data file the person already has, such as the one their other computers use.
		/// </summary>
		/// <returns>The data file, or null when the person closed the question</returns>
		private async Task<string?> ChooseFirstDataFileAsync()
		{
			while (true)
			{
				var answer = await _dialogs.AskAsync(new Question(T.FirstStartTitle, T.FirstStart), T.StartNew, T.OpenMine);
				if (answer == 0)
					return AppPaths.DefaultDataFile;
				if (answer != 1)
					return null;
				if (await _dialogs.PickDataFileAsync() is { } path)
					return path;
			}
		}

		/// <summary>Uses this data file from now on, on this computer</summary>
		private async Task UseDataFileAsync(string path)
		{
			_dataFileOverride = null;
			_settings.DataFilePath = path;
			try
			{
				_settings.Save();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_dialogs.Tell(T.SettingsNotSaved + e.Message, MessageKind.Problem);
			}
			await OpenDataFileAsync();
		}

		/// <summary>Only a history Purity could read and agrees on can move, so nothing stays behind</summary>
		private bool CanMove => _session is { State: SessionState.Ready } && _dataFileOverride == null;

		/// <summary>
		/// Moves the history to another folder, such as one a sync app shares: copies the data file
		/// there, byte for byte, with this computer's backups of it, and uses the copy from then on.
		/// The old file stays as it is, since another computer may still use it, and a file already
		/// in the new place is never replaced, since it may hold someone's history.
		/// </summary>
		private async Task MoveDataFileAsync(string target)
		{
			if (!CanMove || _session == null)
				return;

			var from = Path.GetFullPath(DataFilePath);
			var to = Path.GetFullPath(target);
			if (SamePath(from, to))
				return;
			if (File.Exists(to))
			{
				_dialogs.Tell(T.MoveOntoFile, MessageKind.Problem);
				return;
			}

			if (_session.HasUnsavedChanges)
				await SaveAsync();
			if (_session is not { State: SessionState.Ready, HasUnsavedChanges: false })
			{
				_dialogs.Tell(T.CantMoveYet, MessageKind.Problem);
				return;
			}

			var vault = AppPaths.VaultFor(to);
			var madeVault = false;
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(to)!);
				madeVault = CopyFolder(AppPaths.VaultFor(from), vault);
				if (File.Exists(from))						// a new history has no file until its first period
					CopyExactly(from, to);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				if (madeVault)
					TryDeleteFolder(vault);
				_dialogs.Tell(T.CopyFailed + e.Message, MessageKind.Problem);
				return;
			}

			await UseDataFileAsync(to);
			_dialogs.Tell(T.Moved(to, from), MessageKind.Done);
		}

		/// <exception cref="IOException">The copy failed, or came out different; nothing is left behind</exception>
		private static void CopyExactly(string from, string to)
		{
			File.Copy(from, to, overwrite: false);
			if (File.ReadAllBytes(from).AsSpan().SequenceEqual(File.ReadAllBytes(to)))
				return;

			File.Delete(to);								// only ever the copy just made, since nothing was there
			throw new IOException("The copy came out different from the data file.");
		}

		/// <summary>
		/// Brings this computer's backups and last agreed version along, so Restore still works and
		/// the first save sees no change. Skipped when the new place already has a vault of its own.
		/// Copied into a side folder first, so a failure leaves nothing half done.
		/// </summary>
		/// <returns>Whether it made the new vault</returns>
		private static bool CopyFolder(string from, string to)
		{
			if (!Directory.Exists(from) || Directory.Exists(to))
				return false;

			var side = to + ".copying";
			TryDeleteFolder(side);								// left by a try that failed
			try
			{
				Directory.CreateDirectory(side);
				foreach (var folder in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
					Directory.CreateDirectory(Path.Combine(side, Path.GetRelativePath(from, folder)));
				foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
					File.Copy(file, Path.Combine(side, Path.GetRelativePath(from, file)));
				Directory.Move(side, to);
				return true;
			}
			catch
			{
				TryDeleteFolder(side);
				throw;
			}
		}

		private static void TryDeleteFolder(string folder)
		{
			try
			{
				if (Directory.Exists(folder))
					Directory.Delete(folder, true);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// it's only a vault nothing uses
			}
		}
	}
}

using System.Security.Cryptography;
using Purity.Storage;


namespace Purity.Desktop.ViewModels
{
	/// <summary>
	/// Copies of the data file that a sync app leaves beside it when two devices saved at once, such
	/// as "pureData (Eli's conflicted copy 2026-11-08).json". The WPF app leaves the same kind. Their
	/// entries are missing from the data file until someone brings them in. Bringing a copy in adds
	/// what only it has and asks about anything it disagrees on; a copy can never remove an entry.
	/// Once that is saved, the copy moves to this computer's backups, so the synced folder no longer
	/// holds it and nobody is asked about it again.
	/// </summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>Looks for copies and offers to bring them in, unless the person put them off during this run</summary>
		public Task CheckForCopiesAsync() => BringInCopiesAsync(askAgain: false);


		private async Task BringInCopiesAsync(bool askAgain)
		{
			if (_checkingCopies || _store == null || _session?.State != SessionState.Ready)
				return;

			_checkingCopies = true;
			try
			{
				var found = ReadCopies();
				_copiesWaiting = found.Count;
				var asking = found.Where(el => askAgain || !_copiesPutOff.Contains(el.Key)).ToList();
				if (asking.Count == 0)
					return;

				if (await _dialogs.AskAsync(CopiesQuestion(asking), "Bring them in", "Not now") != 0)
				{
					_copiesPutOff.UnionWith(asking.Select(el => el.Key));
					return;
				}

				var before = Ledger.Data.Count;
				var setAside = 0;
				var problems = new List<string>();
				foreach (var copy in asking)
				{
					if (await BringInAsync(copy) is { } problem)
					{
						problems.Add(problem);
						_copiesPutOff.Add(copy.Key);		// asked again on Refresh, not each time the window comes back
					}
					else
						setAside++;
					if (_session?.State != SessionState.Ready)
						break;					// a question waits, or the file became unreadable
				}

				_copiesWaiting = ReadCopies().Count;
				await _dialogs.AskAsync(CopiesReport(setAside, Ledger.Data.Count - before, problems), "OK");
			}
			finally
			{
				_checkingCopies = false;
				UpdateWarning();
			}
		}

		/// <returns>What went wrong, or null when the copy's entries are saved and the copy is set aside</returns>
		private async Task<string?> BringInAsync(ConflictedCopy copy)
		{
			SaveStatus status;
			try
			{
				status = await _session!.MergeCopyAsync(copy.Content);
			}
			catch (InvalidDataException)
			{
				_copiesPutOff.Add(copy.Key);
				return $"\"{copy.Name}\" can't be read, so it was left where it is.";
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_saveFailed = true;
				return $"\"{copy.Name}\" was left where it is, because the data file couldn't be written.";
			}

			if (status == SaveStatus.NeedsResolution)
			{
				_resolvingCopy = copy;
				if (await ResolveAsync())
					return SetAside(copy);

				return _session?.State == SessionState.NeedsResolution
					? $"\"{copy.Name}\" disagrees with your history about a period. Press Refresh when you're ready to choose."
					: $"\"{copy.Name}\" was left where it is.";
			}

			if (status is SaveStatus.Saved or SaveStatus.Unchanged && _session?.HasUnsavedChanges == false)
				return SetAside(copy);

			_saveFailed = status == SaveStatus.TryAgain;
			return $"\"{copy.Name}\" was left where it is, because the data file couldn't be written.";
		}

		/// <summary>Settles the waiting question, then sets aside the copy that raised it, if one did</summary>
		private async Task ResolvePendingAsync()
		{
			var copy = _resolvingCopy;
			if (!await ResolveAsync() || copy == null)
				return;

			var problem = SetAside(copy);
			if (problem != null)
				_copiesPutOff.Add(copy.Key);
			_copiesWaiting = ReadCopies().Count;
			UpdateWarning();
			await _dialogs.AskAsync(problem ?? $"\"{copy.Name}\" is brought in and now in this computer's backups.", "OK");
		}

		/// <summary>
		/// Moves a copy whose entries are saved into this computer's backups. It stays where it is
		/// unless the backup is written and the file still holds what was brought in.
		/// </summary>
		/// <returns>What went wrong, or null</returns>
		private string? SetAside(ConflictedCopy copy)
		{
			try
			{
				_vault!.AddBackup(copy.Content);
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_copiesPutOff.Add(copy.Key);
				return $"The entries of \"{copy.Name}\" are in your history, but the copy stays beside the data file, because this computer's backups couldn't be written.";
			}

			try
			{
				if (!File.ReadAllBytes(copy.Path).AsSpan().SequenceEqual(copy.Content))
					return $"\"{copy.Name}\" changed while it was being brought in, so it was left where it is.";

				File.Delete(copy.Path);
				return null;
			}
			catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
			{
				return null;							// already gone, removed by its sync app or another device
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_copiesPutOff.Add(copy.Key);
				return $"The entries of \"{copy.Name}\" are in your history, but the copy couldn't be removed from beside the data file.";
			}
		}

		/// <summary>The copies beside the data file, as they are now. One a sync app is still writing is skipped until next time.</summary>
		private List<ConflictedCopy> ReadCopies()
		{
			var copies = new List<ConflictedCopy>();
			IReadOnlyList<string> paths;
			try
			{
				paths = _store?.FindConflictedCopies() ?? Array.Empty<string>();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return copies;
			}

			foreach (var path in paths)
			{
				try
				{
					copies.Add(new ConflictedCopy(path, File.ReadAllBytes(path)));
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					// locked or gone for a moment; the next look finds it
				}
			}
			return copies;
		}

		private void ForgetCopies()
		{
			_copiesPutOff.Clear();
			_copiesWaiting = 0;
			_resolvingCopy = null;
		}

		private static string CopiesQuestion(IReadOnlyList<ConflictedCopy> copies)
		{
			var one = copies.Count == 1;
			var names = string.Join("\n", copies.Take(5).Select(el => "    " + el.Name));
			if (copies.Count > 5)
				names += $"\n    and {copies.Count - 5} more";

			return (one ? "Your sync app left a copy" : $"Your sync app left {copies.Count} copies") +
				" of the data file beside it, from a time when two devices saved at once:\n\n" + names + "\n\n" +
				(one ? "It may hold entries that are missing here. Bring it in?" : "They may hold entries that are missing here. Bring them in?") +
				"\n\nNothing in your history is removed. If a copy disagrees about a period, you choose which version to keep. " +
				"Afterwards each copy moves to this computer's backups.";
		}

		private static string CopiesReport(int setAside, int added, IReadOnlyList<string> problems)
		{
			var lines = new List<string>();
			if (setAside > 0)
			{
				var copies = setAside == 1 ? "1 copy" : $"{setAside} copies";
				var periods = added switch
				{
					0 => setAside == 1 ? "It held nothing new" : "They held nothing new",
					1 => "1 new period came from " + (setAside == 1 ? "it" : "them"),
					_ => $"{added} new periods came from " + (setAside == 1 ? "it" : "them"),
				};
				lines.Add($"Brought in {copies}. {periods}, and {(setAside == 1 ? "it is" : "they are")} now in this computer's backups.");
			}
			lines.AddRange(problems);
			return string.Join("\n\n", lines);
		}


		/// <summary>One copy, read once, so what is brought in is exactly what gets set aside</summary>
		private sealed record ConflictedCopy(string Path, byte[] Content)
		{
			public string Name => System.IO.Path.GetFileName(Path);

			/// <summary>The same file with the same content. A copy that changes is asked about again.</summary>
			public string Key => Path + "|" + Convert.ToHexString(SHA256.HashData(Content));
		}


		private readonly HashSet<string> _copiesPutOff = new();
		private ConflictedCopy? _resolvingCopy;
		private int _copiesWaiting;
		private bool _checkingCopies;
	}
}

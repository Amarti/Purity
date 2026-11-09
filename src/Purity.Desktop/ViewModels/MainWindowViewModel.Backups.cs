using System.Text;
using Purity.Core;
using Purity.Desktop.Localization;
using Purity.Storage;
using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>
	/// Bringing back an earlier version of the history. Every time the data file is replaced, by this
	/// computer or another one, the version it replaced stays in this computer's backups. Restoring
	/// one makes the current version a backup too, so a restore can be undone the same way, and
	/// entries not saved yet join the restored history instead of being lost.
	/// </summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>Whether there is an earlier version to bring back</summary>
		private bool HasBackups()
		{
			try
			{
				return _vault?.ListBackups().Count > 0;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				return false;
			}
		}

		private async Task ChooseAndRestoreAsync()
		{
			if (_session?.State != SessionState.Ready || _vault == null)
				return;

			var choices = _vault.ListBackups().Select(el => new BackupChoice(el, Describe(el))).ToList();
			if (await _dialogs.ChooseBackupAsync(choices) is not { } backup)
				return;

			var when = When(backup);
			var confirm = T.RestoreConfirm(when);
			if (await _dialogs.AskAsync(confirm, T.Restore, T.Cancel) != 0)
				return;

			SaveStatus status;
			try
			{
				_restoringFrom = when;
				status = await _session.RestoreAsync(backup);
				_saveFailed = status == SaveStatus.TryAgain;
			}
			catch (InvalidDataException)
			{
				_restoringFrom = null;
				await _dialogs.AskAsync(T.BackupUnreadable, T.Ok);
				return;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_restoringFrom = null;
				_saveFailed = true;
				UpdateWarning();
				await _dialogs.AskAsync(T.RestoreNotWritten, T.Ok);
				return;
			}

			if (status == SaveStatus.NeedsResolution)
				await ResolvePendingAsync();			// a question put off keeps naming the backup until it's answered
			UpdateWarning();

			if (_session?.State == SessionState.Ready && !_session.HasUnsavedChanges)
				await _dialogs.AskAsync(T.Restored(when), T.Ok);
		}

		/// <summary>When a version was replaced, and what it holds, so the right one can be recognized. Short, for a list.</summary>
		private string Describe(Backup backup)
		{
			var replaced = backup.TakenAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Loc.Instance.DateCulture);
			var text = new StringBuilder(T.Until(replaced));
			try
			{
				var content = _vault!.ReadBackup(backup);
				var periods = PurityFile.Parse(new UTF8Encoding(false).GetString(content));
				if (periods.Count == 0)
					text.Append(T.NoPeriods);
				else
				{
					var last = periods.MaxBy(el => el.Begin)!;
					text.Append(T.PeriodCount(periods.Count))
						.Append(T.LastBeginning(last.Begin.UtcDateTime.ToString("d MMMM yyyy", Loc.Instance.DateCulture)))
						.Append(PurityEvent.IsDateAfterDusk(last.Begin) ? T.NightSuffix : "");
				}

				// an older version can be identical to today's, for example after a restore
				if (content.AsSpan().SequenceEqual(new UTF8Encoding(false).GetBytes(PurityFile.Format(Ledger.Data))))
					text.Append(T.SameAsNow);
				return text.ToString();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or DecoderFallbackException)
			{
				return text.Append(T.CantBeRead).ToString();
			}
		}

		/// <summary>The moment the version was replaced, in this computer's time</summary>
		private static string When(Backup backup) =>
			backup.TakenAt.ToLocalTime().ToString("dddd, d MMMM yyyy, HH:mm", Loc.Instance.DateCulture);


		/// <summary>Names the backup being restored while a question about it is open</summary>
		private string? _restoringFrom;
	}
}

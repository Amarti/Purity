using Purity.Core;
using Purity.Desktop.Localization;
using static Purity.Desktop.Localization.Loc;


namespace Purity.Desktop.ViewModels
{
	/// <summary>
	/// Questions that guard against a slip. Odd dates, most often a typo, are asked about only when a
	/// period is closed: once it's closed, its dates stand as answered, so nothing needs remembering.
	/// Changing a date of a closed period asks first. Neither ever blocks: every date is still worked
	/// out as entered.
	/// </summary>
	public sealed partial class MainWindowViewModel
	{
		/// <summary>
		/// A closed period's dates are already worked out, and later ones build on them, so changing one
		/// asks first, in case it's a slip
		/// </summary>
		/// <param name="start">Whether the start changes, or else the end</param>
		internal async Task<bool> MayChangeClosedAsync(DateTimeOffset was, DateTimeOffset becomes, bool start)
		{
			var body = $"{(start ? T.ChangeClosedStart : T.ChangeClosedEnd)}\n{T.ChangeNow(Onah(was))}\n{T.ChangeNew(Onah(becomes))}\n\n{T.ChangeClosedRefresh}";
			return await _dialogs.AskAsync(new Question(T.ChangeClosedTitle, body), T.Change, T.Cancel) == 0;
		}

		/// <summary>What looks odd about a period about to be closed, or null when nothing does</summary>
		private string? OddText(PurityPeriod period)
		{
			var found = PeriodChecks.Check(Ledger.Data, period);
			return found.Count == 0 ? null : $"{T.OddDates}\n{Bullets(found.Select(Describe))}\n\n{T.OddKeepOpen}";
		}

		private static string Describe(Oddity oddity)
		{
			var date = Loc.Instance.LongDate(oddity.Other);
			return oddity.Kind switch
			{
				OddityKind.EndsBeforeItBegins => T.OddEndsFirst(date),
				OddityKind.VeryLong => T.OddLong(oddity.Days),
				OddityKind.BeginsBeforePreviousEnds => T.OddInPrevious(date),
				OddityKind.EndsAfterNextBegins => T.OddInNext(date),
				_ => T.OddBeforeNewest(date),
			};
		}

		/// <summary>The date with its onah, since a change may be only from day to night</summary>
		private static string Onah(DateTimeOffset stamp) => T.DateWithOnah(Loc.Instance.LongDate(stamp), PurityEvent.IsDateAfterDusk(stamp));
	}
}

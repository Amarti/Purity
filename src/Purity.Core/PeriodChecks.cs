using System;
using System.Collections.Generic;
using System.Linq;


namespace Purity.Core
{
	/// <summary>What can look odd about a period's dates, most often because of a typo</summary>
	public enum OddityKind
	{
		/// <summary>The end comes before the start</summary>
		EndsBeforeItBegins,
		/// <summary>From start to end takes more than <see cref="PeriodChecks.LongestUsualDays"/> days</summary>
		VeryLong,
		/// <summary>It begins before the period before it ends, which makes the interval between them negative</summary>
		BeginsBeforePreviousEnds,
		/// <summary>It ends after the next period begins</summary>
		EndsAfterNextBegins,
		/// <summary>A period not closed yet begins before the newest closed one, so closing it puts it among the earlier ones</summary>
		BeginsBeforeNewest,
	}

	/// <param name="Kind">What looks odd</param>
	/// <param name="Other">The date to show with it: the period's own end, the previous period's end, or another period's start</param>
	/// <param name="Days">How many days the period lasts, for <see cref="OddityKind.VeryLong"/></param>
	public sealed record Oddity(OddityKind Kind, DateTimeOffset Other, int Days = 0);

	/// <summary>
	/// Warnings for odd input. They only read the dates: every date is still worked out as entered,
	/// so nothing here touches the rules or their version.
	/// </summary>
	public static class PeriodChecks
	{
		/// <summary>The most days from start to end that pass without a warning</summary>
		public const int LongestUsualDays = 30;


		/// <summary>What looks odd about one period among the others, as when it's about to be closed</summary>
		public static IReadOnlyList<Oddity> Check(IEnumerable<PurityPeriod> data, PurityPeriod period)
		{
			var others = data.Where(el => el != period).ToList();
			var found = new List<Oddity>();

			var days = Days(period);
			if (period.End < period.Begin)
				found.Add(new(OddityKind.EndsBeforeItBegins, period.End));
			else if (days > LongestUsualDays)
				found.Add(new(OddityKind.VeryLong, period.End, days));

			// Same test as the engine's interval coming out below zero
			var previous = others.Where(el => el.Begin <= period.Begin).MaxBy(el => el.Begin);
			if (previous != null && period.Begin <= previous.EffectiveEnd)
				found.Add(new(OddityKind.BeginsBeforePreviousEnds, previous.End));

			var next = others.Where(el => el.Begin > period.Begin).MinBy(el => el.Begin);
			if (next != null && next.Begin <= period.EffectiveEnd)
				found.Add(new(OddityKind.EndsAfterNextBegins, next.Begin));

			var newest = others.Where(el => el.Closed).MaxBy(el => el.Begin);
			if (!period.Closed && newest != null && period.Begin < newest.Begin)
				found.Add(new(OddityKind.BeginsBeforeNewest, newest.Begin));

			return found;
		}


		/// <summary>Calendar days from the start's date to the end's</summary>
		private static int Days(PurityPeriod period) => (period.End.UtcDateTime.Date - period.Begin.UtcDateTime.Date).Days;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;


namespace Purity.Core
{
	/// <summary>
	/// The history of periods and the rules that run across them. Holds no UI and no I/O, so every
	/// app on every platform shares this one implementation.
	/// </summary>
	public sealed class PeriodLedger
	{
		/// <summary>
		/// Version of the halachic rules. Output may change only with a bump of this number and a
		/// reviewed list of every changed date.
		/// </summary>
		public const int RulesVersion = 1;


		/// <summary>
		/// Takes over a loaded data set. Stored events are kept as they are; only the tracked
		/// intervals are rebuilt.
		/// </summary>
		public void Load(IEnumerable<PurityPeriod> rawData)
		{
			Data = rawData.OrderBy(el => el.Begin).ToList();
			BakeData(false);
		}

		/// <summary>
		/// Performs consistency calculations on whole data set
		/// </summary>
		public void Recalculate() => BakeData(true);

		/// <summary>
		/// Adds a new, still open period, unless one already begins at the same moment
		/// </summary>
		public PurityPeriod? AddPeriod(DateTimeOffset beg, DateTimeOffset end)
		{
			if (Data.Any(el => el.Begin == beg))
				return null;

			var period = new PurityPeriod(beg, end);
			Data.Add(period);
			return period;
		}

		/// <summary>
		/// Accepts a period into the data and recalculates everything
		/// </summary>
		public void AcceptPeriod(PurityPeriod? period)
		{
			if (period == null || period.End == DateTimeOffset.MinValue)
				return;

			period.Closed = true;

			Data = Data.OrderBy(el => el.Begin).ToList();
			BakeData(true);
		}

		/// <summary>
		/// Removes the last period. Any other period is left alone.
		/// </summary>
		/// <returns>Whether the period was removed</returns>
		public bool RemovePeriod(PurityPeriod period)
		{
			if (Data.Count == 0 || period != Data.Last())
				return false;

			Data.Remove(period);
			if (_recentPeriodsStreak.Count != 0 && !period.SkipStreak)
				_recentPeriodsStreak.RemoveAt(_recentPeriodsStreak.Count - 1);
			if (_recentPeriodsStreak.Count == 0)
				BakeData(false);	// need to recalculate _recentPeriodsStreak

			return true;
		}

		/// <summary>
		/// Interval to the previous period in half-calendar days, or 0 for the first period
		/// </summary>
		public int PeriodLength(PurityPeriod period)
		{
			var idx = Data.IndexOf(period);
			return idx > 0 ? PurityPeriod.GetPeriodLength(Data[idx - 1], period) : 0;
		}

		/// <summary>
		/// Text of the Skip option, which shows the interval the option would leave out
		/// </summary>
		public string SkipLabel(PurityPeriod period)
		{
			var length = PeriodLength(period);
			return "Skip" + (length > 0 ? DateText.Invariant($" ({length})") : string.Empty);
		}


		private void BakeData(bool full)
		{
			_recentPeriodsStreak.Clear();

			PurityPeriod? lastPeriod = null;
			foreach (var period in Data)
			{
				UpdateRecentPeriodsStreak(lastPeriod, period);
				if (full)
				{
					period.SubEvents.Clear();
					if (period.End != DateTimeOffset.MinValue && period.Closed)
						period.Commit(_recentPeriodsStreak);
				}
				lastPeriod = period;
			}
		}
		private void UpdateRecentPeriodsStreak(PurityPeriod? a, PurityPeriod b)
		{
			if (a == null)
				return;

			var l = PurityPeriod.GetPeriodLength(a, b);
			if (l == 0)
				return;

			if (!_recentPeriodsStreak.Any() || _recentPeriodsStreak.Last() < l)
				_recentPeriodsStreak.Clear();
			_recentPeriodsStreak.Add(l);
		}


		public List<PurityPeriod> Data { get; private set; } = new();
		public IReadOnlyList<int> TrackedIntervals => _recentPeriodsStreak;

		private readonly List<int> _recentPeriodsStreak = new();
	}
}

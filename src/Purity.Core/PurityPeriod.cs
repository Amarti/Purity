using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;


namespace Purity.Core
{
	public class PurityPeriod
	{
		public PurityPeriod()
		{
			SubEvents = new List<PurityEvent>();
		}
		public PurityPeriod(DateTimeOffset begin, DateTimeOffset end)
			: this()
		{
			Begin = begin;
			End = end;
		}


		/// <summary>
		/// Calculates length between two periods end and begin in half-calendar days (pure half-days)<br/>
		/// Useful for Veset Aflaga calculation
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static int GetPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.End == DateTimeOffset.MinValue || b.Begin == DateTimeOffset.MinValue || b.SkipStreak)	// skipping streak calculation only to upcoming period
				return 0;
			var l = (int)((b.Begin - a.EffectiveEnd).TotalHours / 12) - 1;
			return l;
		}
		/// <summary>
		/// Calculates full period length between two periods begin time in calendar days
		/// </summary>
		/// <param name="a">First period</param>
		/// <param name="b">Second period</param>
		public static float GetFullPeriodLength(PurityPeriod a, PurityPeriod b)
		{
			if (a.End == DateTimeOffset.MinValue || b.Begin == DateTimeOffset.MinValue)
				return 0;
			var l = (float)((b.Begin - a.Begin).TotalHours / 24) - 1;
			return l;
		}


		public void Commit(IReadOnlyList<int> recentPeriodsStreak)
		{
			SubEvents.Clear();

			AddMikveh();
			AddOnaBeinonit();
			AddVesetHodesh();
			AddVesetAflaga(recentPeriodsStreak);
			SubEvents = SubEvents.OrderBy(el => el.Stamp).ToList();
		}
		private void AddMikveh()
		{
			var ed = EffectiveEnd;
			ed = ed.AddDays(7);								// adding one full week
			ed = ed.AddHours(12);							// mikveh is always after dusk
			AddEvent(ed, PurityEventType.Mikveh);
		}
		private void AddOnaBeinonit()
		{
			// if period was after dusk, this is counted as new hebrew date, so by adding 12 more hours we increment gregorian day
			var b = PurityEvent.IsDateAfterDusk(Begin) ? Begin.AddHours(12) : Begin;
			var tm = b.AddDays(7 * 4  + 1);					// adding four full weeks + 1 day
			AddEvent(tm, PurityEventType.OnaBeinonit);
		}
		private void AddVesetHodesh()
		{
			var tm = Calendar.AddMonths(Begin.UtcDateTime, 1);	// adding full hebrew month
			AddEvent(new(tm, TimeSpan.Zero), PurityEventType.VesetHodesh);
		}
		private void AddVesetAflaga(IReadOnlyList<int> recentPeriodsStreak)
		{
			foreach (var p in recentPeriodsStreak)
			{
				var tm = End.AddHours(12 * p);				// adding p half-calendar days
				AddEvent(tm, PurityEventType.VesetAflaga, DateText.Invariant($"({p})"));
			}
		}
		private void AddEvent(DateTimeOffset tm, PurityEventType typ, string? note = null)
		{
			if (!SubEvents.Any(el => el.Stamp == tm && el.Type == typ))
				SubEvents.Add(new PurityEvent(typ, tm, note));
		}


		public DateTimeOffset Begin { get; set; }
		public DateTimeOffset End { get; set; }
		/// <summary>
		/// End date corrected to beginning of next hebrew calendar day.
		/// <para/>If verification (bdikah) is made after dusk, it is considered next hebrew date, since all bdikah is counted in the light of day
		/// </summary>
		[JsonIgnore]
		public DateTimeOffset EffectiveEnd => PurityEvent.IsDateAfterDusk(End) ? End.AddHours(12) : End;
		/// <summary>
		/// Period is closed and needs calculation
		/// </summary>
		public bool Closed { get; set; }
		/// <summary>
		/// Omit this period from Veset Aflaga calculation due to some external reasons (surgery, intrauterine device installation, etc)
		/// </summary>
		public bool SkipStreak { get; set; }
		public List<PurityEvent> SubEvents { get; set; }

		private static readonly HebrewCalendar Calendar = new();
	}
}

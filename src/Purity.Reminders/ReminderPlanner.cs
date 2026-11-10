using System;
using System.Collections.Generic;
using System.Linq;
using Purity.Core;


namespace Purity.Reminders
{
	/// <summary>
	/// Works out which reminders are wanted now. It only reads what the engine calculated. A
	/// reminder's moment is on the local clock, since the dates themselves are symbolic and have
	/// no time of their own.
	/// </summary>
	public static class ReminderPlanner
	{
		/// <summary>The reminders still to come, soonest first</summary>
		/// <param name="periods">The whole history</param>
		/// <param name="options">What to remind about, and how</param>
		/// <param name="now">The time now, on the local clock</param>
		public static IReadOnlyList<Reminder> Plan(IReadOnlyList<PurityPeriod> periods, ReminderOptions options, DateTime now)
		{
			// A period has no dates until it's closed, and once a new one has begun the earlier ones' are over
			if (periods.Count == 0 || periods.Any(el => !el.Closed))
				return Array.Empty<Reminder>();

			var newest = periods.MaxBy(el => el.Begin)!;
			return newest.SubEvents
				.Where(el => options.Kinds.Contains(el.Type))
				.Select(el => ReminderOf(newest, el, options))
				.Where(el => el.Moment >= now)
				.OrderBy(el => el.Moment)
				.ThenBy(el => el.Tag, StringComparer.Ordinal)
				.ToList();
		}


		private static Reminder ReminderOf(PurityPeriod period, PurityEvent item, ReminderOptions options)
		{
			var words = ReminderWords.For(options.Language);
			var date = item.Stamp.UtcDateTime.Date;
			var other = options.OtherTime.ToTimeSpan();
			DateTime moment;
			string when;
			if (item.IsFullDay)								// it begins on the evening before its date
			{
				var evening = date.AddDays(-1);
				moment = evening + other;
				when = words.FullDayFrom(evening.DayOfWeek);
			}
			else if (PurityEvent.IsDateAfterDusk(item.Stamp))
			{
				var mikveh = item.Type == PurityEventType.Mikveh;
				moment = date + (mikveh ? options.MikvehTime.ToTimeSpan() : other);
				when = words.Night(date.DayOfWeek, mikveh);
			}
			else											// a daytime begins before any hour on its own date would help
			{
				moment = date.AddDays(-1) + other;
				when = words.Daylight(date.DayOfWeek);
			}

			if (options.MoveOffShabbat)
				moment = OffShabbat(moment);
			return new Reminder(ReminderTag.Of(period, item), moment, words.Title(item.Type, options.Wording, when), item.Type);
		}

		/// <summary>A reminder from Friday afternoon through Saturday would go unseen, so it fires on Friday morning</summary>
		private static DateTime OffShabbat(DateTime moment) => moment.DayOfWeek switch
		{
			DayOfWeek.Friday when moment.Hour >= FridayAfternoon => moment.Date.AddHours(FridayMorning),
			DayOfWeek.Saturday => moment.Date.AddDays(-1).AddHours(FridayMorning),
			_ => moment,
		};


		private const int FridayAfternoon = 12;
		private const int FridayMorning = 8;
	}
}

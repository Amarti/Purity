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
		public static IReadOnlyList<Reminder> Plan(IReadOnlyList<PurityPeriod> periods, ReminderOptions options, DateTime now) =>
			PlanAll(periods, options).Where(el => el.Moment >= now).ToList();

		/// <summary>Every reminder of the newest period, fired or not, soonest first. A calendar keeps these and loses the rest.</summary>
		/// <param name="periods">The whole history</param>
		/// <param name="options">What to remind about, and how</param>
		public static IReadOnlyList<Reminder> PlanAll(IReadOnlyList<PurityPeriod> periods, ReminderOptions options)
		{
			// A period has no dates until it's closed, and once a new one has begun the earlier ones' are over
			if (periods.Count == 0 || periods.Any(el => !el.Closed))
				return Array.Empty<Reminder>();

			var newest = periods.MaxBy(el => el.Begin)!;
			return newest.SubEvents
				.Where(el => options.Kinds.Contains(el.Type))
				.Select(el => ReminderOf(newest, el, options))
				.OrderBy(el => el.Moment)
				.ThenBy(el => el.Tag, StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>A reminder to prove the alerts arrive, a couple of minutes from now. It isn't about any date, so its kind is never read.</summary>
		public static Reminder TestReminder(ReminderLanguage language, DateTime now)
		{
			var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
			return new Reminder(ReminderTag.Test, minute.AddMinutes(TestDelayMinutes), ReminderWords.For(language).TestTitle, PurityEventType.Mikveh);
		}


		private static Reminder ReminderOf(PurityPeriod period, PurityEvent item, ReminderOptions options)
		{
			var words = ReminderWords.For(options.Language);
			var date = DateTime.SpecifyKind(item.Stamp.UtcDateTime.Date, DateTimeKind.Unspecified);		// a date on the local clock, not a moment in UTC
			var other = options.OtherTime.ToTimeSpan();
			Func<DateTime, DateTime> moved = options.MoveOffShabbat ? el => OffRestDays(el, options.InIsrael) : el => el;
			DateTime moment;
			string when;
			if (item.IsFullDay)								// it begins on the evening before its date
			{
				var evening = date.AddDays(-1);
				moment = moved(evening + other);
				when = words.FullDayFrom(evening.DayOfWeek, moment.Date < evening);
			}
			else if (PurityEvent.IsDateAfterDusk(item.Stamp))
			{
				var mikveh = item.Type == PurityEventType.Mikveh;
				moment = moved(date + (mikveh ? options.MikvehTime.ToTimeSpan() : other));
				when = words.Night(date.DayOfWeek, mikveh, moment.Date < date);
			}
			else											// a daytime begins before any hour on its own date would help
			{
				moment = moved(date.AddDays(-1) + other);
				when = words.Daylight(date.DayOfWeek);
			}

			return new Reminder(ReminderTag.Of(period, item), moment, words.Title(item.Type, options.Wording, when), item.Type);
		}

		/// <summary>
		/// A reminder from the afternoon before Shabbat or Yom Tov until its last day ends would go
		/// unseen, so it fires on the morning before. Where Yom Tov and Shabbat follow one another,
		/// that's the morning before the first of them.
		/// </summary>
		private static DateTime OffRestDays(DateTime moment, bool inIsrael)
		{
			var day = moment.Date;
			if (YomTov.IsRestDay(day, inIsrael))
			{
				var first = day;
				while (YomTov.IsRestDay(first.AddDays(-1), inIsrael))
					first = first.AddDays(-1);
				return first.AddDays(-1).AddHours(EveMorning);
			}
			return moment.Hour >= EveAfternoon && YomTov.IsRestDay(day.AddDays(1), inIsrael) ? day.AddHours(EveMorning) : moment;
		}


		private const int TestDelayMinutes = 2;
		private const int EveAfternoon = 12;
		private const int EveMorning = 8;
	}
}

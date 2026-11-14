using System;
using System.Collections.Generic;
using Purity.Core;


namespace Purity.Reminders
{
	/// <summary>The approved wording of the reminders. Nothing here is the owner's to change by accident: a test pins every line.</summary>
	internal sealed class ReminderWords
	{
		public static ReminderWords For(ReminderLanguage language) => language == ReminderLanguage.Hebrew ? Hebrew : English;


		public string Title(PurityEventType kind, ReminderWording wording, string when)
		{
			var name = (wording == ReminderWording.Discreet ? DiscreetNames : DetailedNames)[kind];
			var gap = wording == ReminderWording.Discreet && kind != PurityEventType.Mikveh ? ", " : " ";
			return name + gap + when;
		}

		/// <summary>A reminder about the night of a date, read on that date or, around Shabbat and Yom Tov, on a morning before</summary>
		/// <param name="earlier">Whether it's read on an earlier day, so "tonight" would be wrong and the night is named</param>
		public string Night(DayOfWeek day, bool mikveh, bool earlier) => day switch
		{
			DayOfWeek.Friday => FridayNight,
			DayOfWeek.Saturday => SaturdayNight,
			_ when earlier => NightOf(day),
			_ => mikveh ? Today : Tonight,
		};

		/// <summary>A reminder about a full day that begins on this evening</summary>
		/// <param name="earlier">Whether it's read on an earlier day, so "tonight" would be wrong and the night is named</param>
		public string FullDayFrom(DayOfWeek evening, bool earlier) => evening switch
		{
			DayOfWeek.Friday => FromFridayNight,
			DayOfWeek.Saturday => FromSaturdayNight,
			_ when earlier => FromNightOf(evening),
			_ => FromTonight,
		};


		private static readonly string[] HebrewDays = { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי" };

		private static readonly ReminderWords English = new()
		{
			DetailedNames = new Dictionary<PurityEventType, string>
			{
				[PurityEventType.Mikveh] = "Mikveh",
				[PurityEventType.VesetHodesh] = "Veset Hachodesh",
				[PurityEventType.VesetAflaga] = "Veset Aflaga",
				[PurityEventType.OnaBeinonit] = "Ona Benonis",
			},
			DiscreetNames = new Dictionary<PurityEventType, string>
			{
				[PurityEventType.Mikveh] = "Appointment",
				[PurityEventType.VesetHodesh] = "Reminder H",
				[PurityEventType.VesetAflaga] = "Reminder A",
				[PurityEventType.OnaBeinonit] = "Reminder O",
			},
			TestTitle = "Test reminder",
			Today = "today",
			Tonight = "tonight",
			FridayNight = "Friday night",
			SaturdayNight = "Saturday night",
			FromTonight = "from tonight, full day",
			FromFridayNight = "from Friday night, full day",
			FromSaturdayNight = "from Saturday night, full day",
			Daylight = day => $"{day} daylight",
			NightOf = day => $"{day} night",
			FromNightOf = day => $"from {day} night, full day",
		};

		private static readonly ReminderWords Hebrew = new()
		{
			DetailedNames = new Dictionary<PurityEventType, string>
			{
				[PurityEventType.Mikveh] = "מקווה",
				[PurityEventType.VesetHodesh] = "וסת החודש",
				[PurityEventType.VesetAflaga] = "וסת ההפלגה",
				[PurityEventType.OnaBeinonit] = "עונה בינונית",
			},
			DiscreetNames = new Dictionary<PurityEventType, string>
			{
				[PurityEventType.Mikveh] = "תור",
				[PurityEventType.VesetHodesh] = "תזכורת ח׳",
				[PurityEventType.VesetAflaga] = "תזכורת פ׳",
				[PurityEventType.OnaBeinonit] = "תזכורת ע׳",
			},
			TestTitle = "תזכורת בדיקה",
			Today = "הערב",
			Tonight = "הלילה",
			FridayNight = "בליל שבת",
			SaturdayNight = "במוצ\"ש",
			FromTonight = "מהערב, יממה שלמה",
			FromFridayNight = "מליל שבת, יממה שלמה",
			FromSaturdayNight = "ממוצ\"ש, יממה שלמה",
			Daylight = day => day == DayOfWeek.Saturday ? "בשבת בשעות היום" : $"ביום {HebrewDays[(int)day]} בשעות היום",
			NightOf = day => $"בליל {HebrewDays[(int)day + 1]}",				// a Hebrew night belongs to the day that follows it, as ליל שבת does
			FromNightOf = day => $"מליל {HebrewDays[(int)day + 1]}, יממה שלמה",
		};


		public required IReadOnlyDictionary<PurityEventType, string> DetailedNames { get; init; }
		public required IReadOnlyDictionary<PurityEventType, string> DiscreetNames { get; init; }
		public required string TestTitle { get; init; }
		public required string Today { get; init; }
		public required string Tonight { get; init; }
		public required string FridayNight { get; init; }
		public required string SaturdayNight { get; init; }
		public required string FromTonight { get; init; }
		public required string FromFridayNight { get; init; }
		public required string FromSaturdayNight { get; init; }

		/// <summary>A reminder about the daytime of a date, always named by its weekday</summary>
		public required Func<DayOfWeek, string> Daylight { get; init; }

		/// <summary>The night after a day from Sunday to Thursday, named for a reminder read on an earlier day because of Yom Tov</summary>
		public required Func<DayOfWeek, string> NightOf { get; init; }

		/// <summary>A full day beginning on the night after a day from Sunday to Thursday, named the same way</summary>
		public required Func<DayOfWeek, string> FromNightOf { get; init; }
	}
}

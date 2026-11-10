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

		/// <summary>A reminder about the night of a date, read on that date or, around Shabbat, on the Friday before</summary>
		public string Night(DayOfWeek day, bool mikveh) => day switch
		{
			DayOfWeek.Friday => FridayNight,
			DayOfWeek.Saturday => SaturdayNight,
			_ => mikveh ? Today : Tonight,
		};

		/// <summary>A reminder about a full day that begins on this evening</summary>
		public string FullDayFrom(DayOfWeek evening) => evening switch
		{
			DayOfWeek.Friday => FromFridayNight,
			DayOfWeek.Saturday => FromSaturdayNight,
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
			Today = "today",
			Tonight = "tonight",
			FridayNight = "Friday night",
			SaturdayNight = "Saturday night",
			FromTonight = "from tonight, full day",
			FromFridayNight = "from Friday night, full day",
			FromSaturdayNight = "from Saturday night, full day",
			Daylight = day => $"{day} daylight",
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
			Today = "הערב",
			Tonight = "הלילה",
			FridayNight = "בליל שבת",
			SaturdayNight = "במוצ\"ש",
			FromTonight = "מהערב, יממה שלמה",
			FromFridayNight = "מליל שבת, יממה שלמה",
			FromSaturdayNight = "ממוצ\"ש, יממה שלמה",
			Daylight = day => day == DayOfWeek.Saturday ? "בשבת בשעות היום" : $"ביום {HebrewDays[(int)day]} בשעות היום",
		};


		public required IReadOnlyDictionary<PurityEventType, string> DetailedNames { get; init; }
		public required IReadOnlyDictionary<PurityEventType, string> DiscreetNames { get; init; }
		public required string Today { get; init; }
		public required string Tonight { get; init; }
		public required string FridayNight { get; init; }
		public required string SaturdayNight { get; init; }
		public required string FromTonight { get; init; }
		public required string FromFridayNight { get; init; }
		public required string FromSaturdayNight { get; init; }

		/// <summary>A reminder about the daytime of a date, always named by its weekday</summary>
		public required Func<DayOfWeek, string> Daylight { get; init; }
	}
}

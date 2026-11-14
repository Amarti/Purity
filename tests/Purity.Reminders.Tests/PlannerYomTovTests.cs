using Purity.Core;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>
	/// Reminders around Yom Tov move as they do around Shabbat. The dates of the reminders are made
	/// up; the holidays are where the calendar puts them in 2031.
	/// </summary>
	public sealed class PlannerYomTovTests
	{
		// Pesach 2031: the eve on Monday 7 April, Yom Tov on Tuesday the 8th, and outside Israel Wednesday the 9th too
		private const string PesachEve = "2031-04-07", Pesach = "2031-04-08", PesachSecond = "2031-04-09";

		// Rosh Hashana 2031 on Thursday 18 and Friday 19 September, then Shabbat: one rest of three days, from Wednesday's eve
		private const string RoshHashanaEve = "2031-09-17", RoshHashana = "2031-09-18", RoshHashanaSecond = "2031-09-19", ShabbatAfter = "2031-09-20", SundayAfter = "2031-09-21";


		[Fact]
		public void TheHolidaysAreOnTheDaysTheTestsSay()
		{
			Assert.False(YomTov.IsRestDay(DateTime.Parse(PesachEve), false));
			Assert.True(YomTov.IsYomTov(DateTime.Parse(Pesach), true));
			Assert.False(YomTov.IsYomTov(DateTime.Parse(PesachSecond), true));
			Assert.True(YomTov.IsYomTov(DateTime.Parse(PesachSecond), false));
			Assert.False(YomTov.IsRestDay(DateTime.Parse(RoshHashanaEve), false));
			Assert.True(YomTov.IsYomTov(DateTime.Parse(RoshHashana), true));
			Assert.True(YomTov.IsYomTov(DateTime.Parse(RoshHashanaSecond), true));
			Assert.Equal(DayOfWeek.Saturday, DateTime.Parse(ShabbatAfter).DayOfWeek);
			Assert.False(YomTov.IsRestDay(DateTime.Parse(SundayAfter), true));
		}

		[Theory]
		// The date's kind, Gregorian date and onah, then the alert and the four wordings
		[InlineData(Mikveh, PesachEve, true, "2031-04-07 08:00", "Mikveh today", "Appointment today", "מקווה הערב", "תור הערב")]
		[InlineData(Aflaga, PesachEve, true, "2031-04-07 08:00", "Veset Aflaga tonight", "Reminder A, tonight", "וסת ההפלגה הלילה", "תזכורת פ׳, הלילה")]
		[InlineData(Mikveh, Pesach, true, "2031-04-07 08:00", "Mikveh Tuesday night", "Appointment Tuesday night", "מקווה בליל רביעי", "תור בליל רביעי")]
		[InlineData(Aflaga, Pesach, true, "2031-04-07 08:00", "Veset Aflaga Tuesday night", "Reminder A, Tuesday night", "וסת ההפלגה בליל רביעי", "תזכורת פ׳, בליל רביעי")]
		[InlineData(Ona, PesachSecond, false, "2031-04-07 08:00", "Ona Benonis from Tuesday night, full day", "Reminder O, from Tuesday night, full day", "עונה בינונית מליל רביעי, יממה שלמה", "תזכורת ע׳, מליל רביעי, יממה שלמה")]
		[InlineData(Hachodesh, Pesach, false, "2031-04-07 08:00", "Veset Hachodesh Tuesday daylight", "Reminder H, Tuesday daylight", "וסת החודש ביום שלישי בשעות היום", "תזכורת ח׳, ביום שלישי בשעות היום")]
		[InlineData(Hachodesh, PesachSecond, false, "2031-04-07 08:00", "Veset Hachodesh Wednesday daylight", "Reminder H, Wednesday daylight", "וסת החודש ביום רביעי בשעות היום", "תזכורת ח׳, ביום רביעי בשעות היום")]
		public void AroundYomTovInIsrael(int kind, string date, bool night, string moment, string detailed, string discreet, string hebrewDetailed, string hebrewDiscreet)
		{
			var item = Event((PurityEventType)kind, date, night);
			Assert.Equal(DateTime.Parse(moment), Only(item).Moment);

			Assert.Equal(detailed, Only(item, ReminderLanguage.English, ReminderWording.Detailed).Title);
			Assert.Equal(discreet, Only(item, ReminderLanguage.English, ReminderWording.Discreet).Title);
			Assert.Equal(hebrewDetailed, Only(item, ReminderLanguage.Hebrew, ReminderWording.Detailed).Title);
			Assert.Equal(hebrewDiscreet, Only(item, ReminderLanguage.Hebrew, ReminderWording.Discreet).Title);
		}

		[Theory]
		[InlineData(true, "2031-04-09 08:00", "Appointment today")]
		[InlineData(false, "2031-04-07 08:00", "Appointment Wednesday night")]
		public void TheSecondDayCountsOnlyOutsideIsrael(bool inIsrael, string moment, string title)
		{
			var reminder = Only(Event(PurityEventType.Mikveh, PesachSecond, true), inIsrael: inIsrael);

			Assert.Equal(DateTime.Parse(moment), reminder.Moment);
			Assert.Equal(title, reminder.Title);
		}

		[Theory]
		[InlineData(Mikveh, RoshHashana, true, "Appointment Thursday night")]
		[InlineData(Mikveh, RoshHashanaSecond, true, "Appointment Friday night")]
		[InlineData(Mikveh, ShabbatAfter, true, "Appointment Saturday night")]
		[InlineData(Hachodesh, SundayAfter, false, "Reminder H, Sunday daylight")]
		[InlineData(Ona, SundayAfter, false, "Reminder O, from Saturday night, full day")]
		[InlineData(Aflaga, RoshHashanaEve, true, "Reminder A, tonight")]
		public void YomTovRunningIntoShabbatMovesToTheMorningBeforeBoth(int kind, string date, bool night, string title)
		{
			var reminder = Only(Event((PurityEventType)kind, date, night));

			Assert.Equal(DateTime.Parse(RoshHashanaEve).AddHours(8), reminder.Moment);
			Assert.Equal(title, reminder.Title);
		}

		[Fact]
		public void TheMorningOfTheEveStaysPut()
		{
			var options = Options() with { MikvehTime = new TimeOnly(11, 30) };

			Assert.Equal(DateTime.Parse("2031-04-07 11:30"), Plan(Event(PurityEventType.Mikveh, PesachEve, true), options).Moment);
		}

		[Fact]
		public void WithoutTheMoveNothingMovesAndNothingIsRenamed()
		{
			var reminder = Plan(Event(PurityEventType.Mikveh, Pesach, true), Options() with { MoveOffShabbat = false });

			Assert.Equal(DateTime.Parse("2031-04-08 08:00"), reminder.Moment);
			Assert.Equal("Appointment today", reminder.Title);
		}

		[Theory]
		[InlineData(DayOfWeek.Sunday, "Sunday night", "בליל שני", "from Sunday night, full day", "מליל שני, יממה שלמה")]
		[InlineData(DayOfWeek.Monday, "Monday night", "בליל שלישי", "from Monday night, full day", "מליל שלישי, יממה שלמה")]
		[InlineData(DayOfWeek.Tuesday, "Tuesday night", "בליל רביעי", "from Tuesday night, full day", "מליל רביעי, יממה שלמה")]
		[InlineData(DayOfWeek.Wednesday, "Wednesday night", "בליל חמישי", "from Wednesday night, full day", "מליל חמישי, יממה שלמה")]
		[InlineData(DayOfWeek.Thursday, "Thursday night", "בליל שישי", "from Thursday night, full day", "מליל שישי, יממה שלמה")]
		public void ANightReadOnAnEarlierDayIsNamedByTheDayItBegins(DayOfWeek day, string english, string hebrew, string englishFull, string hebrewFull)
		{
			var en = ReminderWords.For(ReminderLanguage.English);
			var he = ReminderWords.For(ReminderLanguage.Hebrew);

			Assert.Equal(english, en.Night(day, mikveh: true, earlier: true));
			Assert.Equal(hebrew, he.Night(day, mikveh: false, earlier: true));
			Assert.Equal(englishFull, en.FullDayFrom(day, earlier: true));
			Assert.Equal(hebrewFull, he.FullDayFrom(day, earlier: true));
		}


		private const int Mikveh = 1, Hachodesh = 2, Aflaga = 3, Ona = 4;

		private static ReminderOptions Options(ReminderLanguage language = ReminderLanguage.English, ReminderWording wording = ReminderWording.Discreet, bool inIsrael = true) => new()
		{
			Kinds = Enum.GetValues<PurityEventType>().ToHashSet(),
			Language = language,
			Wording = wording,
			InIsrael = inIsrael,
		};

		private static PurityEvent Event(PurityEventType kind, string date, bool night) =>
			new(kind, new DateTimeOffset(DateTime.Parse(date).AddHours(night ? 12 : 0), TimeSpan.Zero));

		private static Reminder Plan(PurityEvent item, ReminderOptions options)
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.Add(item);
			return ReminderPlanner.Plan(new[] { period }, options, new DateTime(2030, 1, 1)).Single();
		}

		private static Reminder Only(PurityEvent item, ReminderLanguage language = ReminderLanguage.English, ReminderWording wording = ReminderWording.Discreet, bool inIsrael = true) =>
			Plan(item, Options(language, wording, inIsrael));
	}
}

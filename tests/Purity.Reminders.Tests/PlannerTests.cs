using Purity.Core;
using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>The approved table of alerts and wording, line by line. Every date is made up.</summary>
	public sealed class PlannerTests
	{
		// 2 March 2031 is a Sunday, and the week runs to Sunday the 9th
		private const string Sun = "2031-03-02", Mon = "2031-03-03", Fri = "2031-03-07", Sat = "2031-03-08", NextSun = "2031-03-09";

		private const int Mikveh = 1, Hachodesh = 2, Aflaga = 3, Ona = 4;


		[Theory]
		// The date's kind, Gregorian date and onah, then the alert and the four wordings. Numbers are the table's rows.
		/* 1*/ [InlineData(Mikveh, Sun, true, "2031-03-02 08:00", "Mikveh today", "Appointment today", "מקווה הערב", "תור הערב")]
		/* 2*/ [InlineData(Mikveh, Fri, true, "2031-03-07 08:00", "Mikveh Friday night", "Appointment Friday night", "מקווה בליל שבת", "תור בליל שבת")]
		/* 3*/ [InlineData(Mikveh, Sat, true, "2031-03-07 08:00", "Mikveh Saturday night", "Appointment Saturday night", "מקווה במוצ\"ש", "תור במוצ\"ש")]
		/* 4*/ [InlineData(Hachodesh, Sun, true, "2031-03-02 17:00", "Veset Hachodesh tonight", "Reminder H, tonight", "וסת החודש הלילה", "תזכורת ח׳, הלילה")]
		/* 5*/ [InlineData(Hachodesh, Fri, true, "2031-03-07 08:00", "Veset Hachodesh Friday night", "Reminder H, Friday night", "וסת החודש בליל שבת", "תזכורת ח׳, בליל שבת")]
		/* 6*/ [InlineData(Hachodesh, Sat, true, "2031-03-07 08:00", "Veset Hachodesh Saturday night", "Reminder H, Saturday night", "וסת החודש במוצ\"ש", "תזכורת ח׳, במוצ\"ש")]
		/* 7*/ [InlineData(Hachodesh, Mon, false, "2031-03-02 17:00", "Veset Hachodesh Monday daylight", "Reminder H, Monday daylight", "וסת החודש ביום שני בשעות היום", "תזכורת ח׳, ביום שני בשעות היום")]
		/* 8*/ [InlineData(Hachodesh, Sat, false, "2031-03-07 08:00", "Veset Hachodesh Saturday daylight", "Reminder H, Saturday daylight", "וסת החודש בשבת בשעות היום", "תזכורת ח׳, בשבת בשעות היום")]
		/* 9*/ [InlineData(Hachodesh, NextSun, false, "2031-03-07 08:00", "Veset Hachodesh Sunday daylight", "Reminder H, Sunday daylight", "וסת החודש ביום ראשון בשעות היום", "תזכורת ח׳, ביום ראשון בשעות היום")]
		/*10*/ [InlineData(Aflaga, Sun, true, "2031-03-02 17:00", "Veset Aflaga tonight", "Reminder A, tonight", "וסת ההפלגה הלילה", "תזכורת פ׳, הלילה")]
		/*11*/ [InlineData(Aflaga, Fri, true, "2031-03-07 08:00", "Veset Aflaga Friday night", "Reminder A, Friday night", "וסת ההפלגה בליל שבת", "תזכורת פ׳, בליל שבת")]
		/*12*/ [InlineData(Aflaga, Sat, true, "2031-03-07 08:00", "Veset Aflaga Saturday night", "Reminder A, Saturday night", "וסת ההפלגה במוצ\"ש", "תזכורת פ׳, במוצ\"ש")]
		/*13*/ [InlineData(Aflaga, Mon, false, "2031-03-02 17:00", "Veset Aflaga Monday daylight", "Reminder A, Monday daylight", "וסת ההפלגה ביום שני בשעות היום", "תזכורת פ׳, ביום שני בשעות היום")]
		/*14*/ [InlineData(Aflaga, Sat, false, "2031-03-07 08:00", "Veset Aflaga Saturday daylight", "Reminder A, Saturday daylight", "וסת ההפלגה בשבת בשעות היום", "תזכורת פ׳, בשבת בשעות היום")]
		/*15*/ [InlineData(Aflaga, NextSun, false, "2031-03-07 08:00", "Veset Aflaga Sunday daylight", "Reminder A, Sunday daylight", "וסת ההפלגה ביום ראשון בשעות היום", "תזכורת פ׳, ביום ראשון בשעות היום")]
		/*16*/ [InlineData(Ona, Mon, false, "2031-03-02 17:00", "Ona Benonis from tonight, full day", "Reminder O, from tonight, full day", "עונה בינונית מהערב, יממה שלמה", "תזכורת ע׳, מהערב, יממה שלמה")]
		/*17*/ [InlineData(Ona, Sat, false, "2031-03-07 08:00", "Ona Benonis from Friday night, full day", "Reminder O, from Friday night, full day", "עונה בינונית מליל שבת, יממה שלמה", "תזכורת ע׳, מליל שבת, יממה שלמה")]
		/*18*/ [InlineData(Ona, NextSun, false, "2031-03-07 08:00", "Ona Benonis from Saturday night, full day", "Reminder O, from Saturday night, full day", "עונה בינונית ממוצ\"ש, יממה שלמה", "תזכורת ע׳, ממוצ\"ש, יממה שלמה")]
		public void EveryLineOfTheApprovedTable(int kind, string date, bool night, string moment, string detailed, string discreet, string hebrewDetailed, string hebrewDiscreet)
		{
			var item = Event((PurityEventType)kind, date, night);
			Assert.Equal(DateTime.Parse(moment), Only(item).Moment);

			Assert.Equal(detailed, Only(item, ReminderLanguage.English, ReminderWording.Detailed).Title);
			Assert.Equal(discreet, Only(item, ReminderLanguage.English, ReminderWording.Discreet).Title);
			Assert.Equal(hebrewDetailed, Only(item, ReminderLanguage.Hebrew, ReminderWording.Detailed).Title);
			Assert.Equal(hebrewDiscreet, Only(item, ReminderLanguage.Hebrew, ReminderWording.Discreet).Title);
		}

		[Fact]
		public void TheWeekInTheTableIsTheOneItSaysItIs()
		{
			Assert.Equal(DayOfWeek.Sunday, DateTime.Parse(Sun).DayOfWeek);
			Assert.Equal(DayOfWeek.Friday, DateTime.Parse(Fri).DayOfWeek);
			Assert.Equal(DayOfWeek.Saturday, DateTime.Parse(Sat).DayOfWeek);
			Assert.Equal(DayOfWeek.Sunday, DateTime.Parse(NextSun).DayOfWeek);
		}

		[Theory]
		[InlineData("2031-03-02")]
		[InlineData("2031-03-03")]
		[InlineData("2031-03-04")]
		[InlineData("2031-03-05")]
		[InlineData("2031-03-06")]
		public void SundayToThursdayNightsReadTheSame(string date)
		{
			var mikveh = Only(Event(PurityEventType.Mikveh, date, true));
			Assert.Equal(DateTime.Parse(date).AddHours(8), mikveh.Moment);
			Assert.Equal("Appointment today", mikveh.Title);

			var veset = Only(Event(PurityEventType.VesetAflaga, date, true));
			Assert.Equal(DateTime.Parse(date).AddHours(17), veset.Moment);
			Assert.Equal("Reminder A, tonight", veset.Title);

			var full = Only(Event(PurityEventType.OnaBeinonit, DateTime.Parse(date).AddDays(1).ToString("yyyy-MM-dd"), false));
			Assert.Equal(DateTime.Parse(date).AddHours(17), full.Moment);
			Assert.Equal("Reminder O, from tonight, full day", full.Title);
		}

		[Theory]
		[InlineData("2031-03-03", "Monday", "שני")]
		[InlineData("2031-03-04", "Tuesday", "שלישי")]
		[InlineData("2031-03-05", "Wednesday", "רביעי")]
		[InlineData("2031-03-06", "Thursday", "חמישי")]
		[InlineData("2031-03-07", "Friday", "שישי")]
		public void AWeekdayDaytimeIsRemindedTheDayBeforeByName(string date, string english, string hebrew)
		{
			var item = Event(PurityEventType.VesetHodesh, date, false);

			Assert.Equal(DateTime.Parse(date).AddDays(-1).AddHours(17), Only(item).Moment);
			Assert.Equal($"Reminder H, {english} daylight", Only(item).Title);
			Assert.Equal($"וסת החודש ביום {hebrew} בשעות היום", Only(item, ReminderLanguage.Hebrew, ReminderWording.Detailed).Title);
		}

		[Fact]
		public void NoLineEverSaysTomorrow()
		{
			foreach (var language in Enum.GetValues<ReminderLanguage>())
				foreach (var wording in Enum.GetValues<ReminderWording>())
					foreach (var kind in Enum.GetValues<PurityEventType>())
						for (var date = DateTime.Parse(Sun); date <= DateTime.Parse(NextSun); date = date.AddDays(1))
							foreach (var night in new[] { false, true })
							{
								var title = Only(Event(kind, date.ToString("yyyy-MM-dd"), night), language, wording).Title;
								Assert.DoesNotContain("tomorrow", title, StringComparison.OrdinalIgnoreCase);
								Assert.DoesNotContain("מחר", title);
							}
		}

		[Theory]
		[InlineData(Mikveh, Sat, true, "2031-03-08 08:00")]
		[InlineData(Hachodesh, Fri, true, "2031-03-07 17:00")]
		[InlineData(Aflaga, NextSun, false, "2031-03-08 17:00")]
		[InlineData(Ona, NextSun, false, "2031-03-08 17:00")]
		public void WithoutTheMoveTheyFireWhenTheyWouldHave(int kind, string date, bool night, string moment)
		{
			var moved = Only(Event((PurityEventType)kind, date, night));
			var staying = Plan(new[] { Closed(Event((PurityEventType)kind, date, night)) }, Options() with { MoveOffShabbat = false }).Single();

			Assert.Equal(DateTime.Parse(moment), staying.Moment);
			Assert.Equal(moved.Title, staying.Title);
		}

		[Fact]
		public void ChosenTimesAreUsed()
		{
			var options = Options() with { MikvehTime = new TimeOnly(9, 30), OtherTime = new TimeOnly(16, 0) };

			Assert.Equal(DateTime.Parse("2031-03-03 09:30"), Plan(new[] { Closed(Event(PurityEventType.Mikveh, Mon, true)) }, options).Single().Moment);
			Assert.Equal(DateTime.Parse("2031-03-03 16:00"), Plan(new[] { Closed(Event(PurityEventType.VesetAflaga, Mon, true)) }, options).Single().Moment);
		}

		[Theory]
		[InlineData(11, 59, "2031-03-07 11:59")]
		[InlineData(12, 0, "2031-03-07 08:00")]
		[InlineData(13, 30, "2031-03-07 08:00")]
		public void FridayMorningStaysAndFridayAfternoonMoves(int hour, int minute, string moment)
		{
			var options = Options() with { MikvehTime = new TimeOnly(hour, minute) };

			Assert.Equal(DateTime.Parse(moment), Plan(new[] { Closed(Event(PurityEventType.Mikveh, Fri, true)) }, options).Single().Moment);
		}

		[Fact]
		public void ADateCrossesIntoTheMonthAndYearBefore()
		{
			Assert.Equal(DateTime.Parse("2030-12-31 17:00"), Only(Event(PurityEventType.OnaBeinonit, "2031-01-01", false)).Moment);
			Assert.Equal(DateTime.Parse("2031-02-28 08:00"), Only(Event(PurityEventType.VesetHodesh, "2031-03-02", false)).Moment);
		}

		[Fact]
		public void TheLocalClockHasNoJumpsToFallInto()
		{
			// Clocks change on these dates in Israel and the US. Nothing here converts between zones, so the hour stays put.
			Assert.Equal(DateTime.Parse("2031-03-28 08:00"), Only(Event(PurityEventType.Mikveh, "2031-03-29", true)).Moment);
			Assert.Equal(DateTime.Parse("2031-03-10 17:00"), Only(Event(PurityEventType.VesetAflaga, "2031-03-10", true)).Moment);
		}


		[Fact]
		public void OnlyTheNewestPeriodHasReminders()
		{
			var older = new PurityPeriod(new DateTimeOffset(2031, 1, 5, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 1, 10, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			older.SubEvents.Add(Event(PurityEventType.VesetHodesh, Mon, true));
			var newest = Closed(Event(PurityEventType.Mikveh, Sun, true));

			var reminders = Plan(new[] { older, newest }, Options());

			Assert.Equal(PurityEventType.Mikveh, reminders.Single().Kind);
		}

		[Fact]
		public void AnOpenPeriodMeansNoRemindersAtAll()
		{
			var closed = Closed(Event(PurityEventType.Mikveh, Sun, true));
			var open = new PurityPeriod(new DateTimeOffset(2031, 2, 25, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero));

			Assert.Empty(Plan(new[] { closed, open }, Options()));
			Assert.Single(Plan(new[] { closed }, Options()));				// taking the open one away brings them back
		}

		[Fact]
		public void NoHistoryMeansNoReminders() => Assert.Empty(Plan(Array.Empty<PurityPeriod>(), Options()));

		[Fact]
		public void WhatHasPassedIsLeftOut()
		{
			var period = Closed(Event(PurityEventType.Mikveh, Sun, true), Event(PurityEventType.Mikveh, Mon, true));

			Assert.Equal(2, Plan(new[] { period }, Options(), DateTime.Parse("2031-03-02 08:00")).Count);	// the very moment still counts
			Assert.Single(Plan(new[] { period }, Options(), DateTime.Parse("2031-03-02 08:01")));
		}

		[Fact]
		public void OnlyTheChosenKindsAreIncludedSoonestFirst()
		{
			var period = Closed(Event(PurityEventType.VesetAflaga, Mon, true), Event(PurityEventType.Mikveh, Sun, true), Event(PurityEventType.VesetHodesh, Mon, true));

			var chosen = Plan(new[] { period }, Options() with { Kinds = new HashSet<PurityEventType> { PurityEventType.Mikveh, PurityEventType.VesetAflaga } });

			Assert.Equal(new[] { PurityEventType.Mikveh, PurityEventType.VesetAflaga }, chosen.Select(el => el.Kind));
		}

		[Fact]
		public void TheEngineIsLeftExactlyAsItWas()
		{
			var ledger = new PeriodLedger();
			foreach (var begin in new[] { new DateTime(2031, 1, 5), new DateTime(2031, 2, 3), new DateTime(2031, 3, 4) })
				ledger.AcceptPeriod(ledger.AddPeriod(Stamps.FromDate(begin, false), Stamps.FromDate(begin.AddDays(5), false)));
			var before = PurityFile.Format(ledger.Data);

			var reminders = ReminderPlanner.Plan(ledger.Data, Options(), DateTime.Parse("2031-01-01"));

			Assert.Equal(ledger.Data[^1].SubEvents.Count, reminders.Count);
			Assert.Equal(before, PurityFile.Format(ledger.Data));
		}


		private static ReminderOptions Options(ReminderLanguage language = ReminderLanguage.English, ReminderWording wording = ReminderWording.Discreet) => new()
		{
			Kinds = Enum.GetValues<PurityEventType>().ToHashSet(),
			Language = language,
			Wording = wording,
		};

		private static PurityEvent Event(PurityEventType kind, string date, bool night) =>
			new(kind, new DateTimeOffset(DateTime.Parse(date).AddHours(night ? 12 : 0), TimeSpan.Zero));

		private static PurityPeriod Closed(params PurityEvent[] items)
		{
			var period = new PurityPeriod(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2031, 2, 6, 0, 0, 0, TimeSpan.Zero)) { Closed = true };
			period.SubEvents.AddRange(items);
			return period;
		}

		private static IReadOnlyList<Reminder> Plan(IReadOnlyList<PurityPeriod> periods, ReminderOptions options, DateTime? now = null) =>
			ReminderPlanner.Plan(periods, options, now ?? new DateTime(2030, 1, 1));

		private static Reminder Only(PurityEvent item, ReminderLanguage language = ReminderLanguage.English, ReminderWording wording = ReminderWording.Discreet) =>
			Plan(new[] { Closed(item) }, Options(language, wording)).Single();
	}
}

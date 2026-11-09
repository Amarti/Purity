using Xunit;


namespace Purity.Parity
{
	/// <summary>
	/// Layer 7: known traps written out by hand with expected values, so a reader can see what the
	/// engine does. Dates are synthetic on purpose: the repo is public.
	/// Tests named Preserved pin behavior that is under halachic review (H1 to H4 in the plan). They
	/// document today's output and aren't a statement that it's correct. Changing one needs a rules
	/// version bump.
	/// </summary>
	public class EdgeCaseTests
	{
		private const int Mikveh = 1, VesetHodesh = 2, VesetAflaga = 3, OnaBeinonit = 4;

		private static DateTimeOffset At(string stamp) => DateTimeOffset.Parse(stamp + "+00:00", System.Globalization.CultureInfo.InvariantCulture);

		private static string Iso(string stamp) => At(stamp).ToString("o");

		private static PeriodInput Period(string begin, string end, bool skip = false) => new(At(begin), At(end), true, skip);

		private static EventDump Single(PeriodInput input, int type, params int[] tracked) =>
			Culture.Run(Culture.Production, () => Engines.Candidate.CommitSingle(input, tracked, true).Single(el => el.Type == type));


		[Fact]
		public void MikvehIsSevenDaysAfterEndAtNight()
		{
			var e = Single(Period("2033-05-01T00:00:00", "2033-05-07T00:00:00"), Mikveh);

			Assert.Equal(Iso("2033-05-14T12:00:00"), e.Stamp);
			Assert.True(e.Dusk);
		}

		[Fact]
		public void MikvehCountsFromNextDayWhenEndIsAtNight()
		{
			var e = Single(Period("2033-05-01T00:00:00", "2033-05-07T12:00:00"), Mikveh);

			Assert.Equal(Iso("2033-05-15T12:00:00"), e.Stamp);
		}

		[Fact]
		public void OnaBeinonitIsDayThirty()
		{
			var e = Single(Period("2033-05-01T00:00:00", "2033-05-07T00:00:00"), OnaBeinonit);

			Assert.Equal(Iso("2033-05-30T00:00:00"), e.Stamp);
			Assert.True(e.FullDay);
		}

		[Fact]
		public void OnaBeinonitAfterNightOnsetStartsFromTheNextDate()
		{
			var e = Single(Period("2033-05-01T12:00:00", "2033-05-07T00:00:00"), OnaBeinonit);

			Assert.Equal(Iso("2033-05-31T00:00:00"), e.Stamp);
		}

		[Theory]
		[InlineData("2033-01-31T00:00:00", "28 Feb - 1 Mar ")]
		[InlineData("2032-01-31T00:00:00", "28-29 Feb ")]
		[InlineData("2034-11-03T00:00:00", "1-2 Dec ")]
		public void OnaBeinonitTextSpansTheNightBefore(string begin, string text)
		{
			var e = Single(Period(begin, "2035-01-01T00:00:00"), OnaBeinonit);

			Assert.Equal(text, e.GregorianText);
		}

		[Fact]
		public void SeptemberIsAbbreviatedAsOnTheProductionMachine()
		{
			var e = Single(Period("2033-08-07T00:00:00", "2033-08-13T00:00:00"), OnaBeinonit);

			Assert.Equal("4-5 Sept ", e.GregorianText);
		}

		[Fact]
		public void VesetHodeshKeepsTheHebrewDateAndOnah()
		{
			var day = Single(Period("2033-05-01T00:00:00", "2033-05-07T00:00:00"), VesetHodesh);
			var night = Single(Period("2033-05-02T12:00:00", "2033-05-07T00:00:00"), VesetHodesh);

			Assert.True(day.Dawn);
			Assert.True(night.Dusk);
		}

		[Fact]
		public void VesetHodeshMovesFromAdarOneToAdarTwoInALeapYear()
		{
			var e = Single(Period("2035-02-19T00:00:00", "2035-02-25T00:00:00"), VesetHodesh);	// 10 Adar I 5795

			Assert.Equal(Iso("2035-03-21T00:00:00"), e.Stamp);											// 10 Adar II 5795
			Assert.Equal("י' אדר ב׳", e.HebrewText);
		}

		/// <summary>H1: onset on the night of 1 Shevat lands on 30 Shevat, not 1 Adar.</summary>
		[Fact]
		public void Preserved_H1_NightOnsetOnTheFirstLandsOnTheThirtieth()
		{
			var e = Single(Period("2031-01-24T12:00:00", "2031-01-30T00:00:00"), VesetHodesh);

			Assert.Equal(Iso("2031-02-22T12:00:00"), e.Stamp);
			Assert.Equal("ל' שבט", e.HebrewText);
		}

		/// <summary>H1 again at the year boundary: Elul always has 29 days.</summary>
		[Fact]
		public void Preserved_H1_NightOnsetOnRoshHashana()
		{
			var e = Single(Period("2032-09-05T12:00:00", "2032-09-11T00:00:00"), VesetHodesh);

			Assert.Equal(Iso("2032-10-04T12:00:00"), e.Stamp);
			Assert.Equal("ל' תשרי", e.HebrewText);
		}

		/// <summary>H2: the same Hebrew date gives different answers by onah when the next month has no 30th.</summary>
		[Fact]
		public void Preserved_H2_ThirtiethDependsOnTheOnah()
		{
			var night = Single(Period("2031-02-22T12:00:00", "2031-02-28T00:00:00"), VesetHodesh);
			var day = Single(Period("2031-02-23T00:00:00", "2031-03-01T00:00:00"), VesetHodesh);

			Assert.Equal(Iso("2031-03-24T12:00:00"), night.Stamp);
			Assert.Equal(Iso("2031-03-24T00:00:00"), day.Stamp);
			Assert.Equal("א' ניסן", night.HebrewText);
			Assert.Equal("כ\"ט אדר", day.HebrewText);
		}

		/// <summary>H3: a longer interval clears everything tracked, including longer ones not yet reached.</summary>
		[Fact]
		public void Preserved_H3_LongerIntervalClearsAllTracked()
		{
			var history = new[]
			{
				Period("2033-01-01T00:00:00", "2033-01-07T00:00:00"),
				Period("2033-01-30T12:00:00", "2033-02-05T00:00:00"),	// interval 46
				Period("2033-02-25T12:00:00", "2033-03-03T00:00:00"),	// interval 40
				Period("2033-03-18T12:00:00", "2033-03-24T00:00:00"),	// interval 30
				Period("2033-04-15T00:00:00", "2033-04-21T00:00:00"),	// interval 43
			};

			var run = Culture.Run(Culture.Production, () => Engines.Candidate.Recalculate(history));

			Assert.Equal([0, 46, 40, 30, 43], run.Periods.Select(el => el.Length));
			Assert.Equal(["(46)", "(40)", "(30)"], run.Periods[3].Events.Where(el => el.Type == VesetAflaga).Select(el => el.Note).Reverse());
			Assert.Equal(["(43)"], run.Periods[4].Events.Where(el => el.Type == VesetAflaga).Select(el => el.Note));
			Assert.Equal("43", run.Tracked);
		}

		/// <summary>H4: the interval is measured from the effective End but projected from the raw End.</summary>
		[Fact]
		public void Preserved_H4_FlagaProjectsFromRawEnd()
		{
			var e = Single(Period("2033-05-01T00:00:00", "2033-05-07T12:00:00"), VesetAflaga, 30);

			Assert.Equal(Iso("2033-05-22T12:00:00"), e.Stamp);
			Assert.Equal("(30)", e.Note);
		}

		[Fact]
		public void SkippedPeriodDoesNotChangeTrackedIntervals()
		{
			var history = new[]
			{
				Period("2033-01-01T00:00:00", "2033-01-07T00:00:00"),
				Period("2033-01-30T12:00:00", "2033-02-05T00:00:00"),				// interval 46
				Period("2033-02-15T00:00:00", "2033-02-20T00:00:00", skip: true),	// not counted
			};

			var run = Culture.Run(Culture.Production, () => Engines.Candidate.Recalculate(history));

			Assert.Equal(0, run.Periods[2].Length);
			Assert.Equal("46", run.Tracked);
		}

		/// <summary>A year typo in End gives a negative interval. Preserved until input warnings arrive.</summary>
		[Fact]
		public void Preserved_YearTypoInEndGivesANegativeInterval()
		{
			var history = new[]
			{
				Period("2033-06-01T00:00:00", "2034-06-07T00:00:00"),
				Period("2033-07-01T00:00:00", "2033-07-07T00:00:00"),
			};

			var run = Culture.Run(Culture.Production, () => Engines.Candidate.Recalculate(history));

			Assert.Equal(-683, run.Periods[1].Length);
			Assert.Contains(run.Periods[1].Events, el => el.Note == "(-683)");
		}

		[Fact]
		public void OpenPeriodHasNoEvents()
		{
			var history = new[]
			{
				Period("2033-01-01T00:00:00", "2033-01-07T00:00:00"),
				new PeriodInput(At("2033-01-30T12:00:00"), At("2033-02-05T00:00:00"), false, false),
			};

			var run = Culture.Run(Culture.Production, () => Engines.Candidate.Recalculate(history));

			Assert.Empty(run.Periods[1].Events);
		}

		[Fact]
		public void SameDayAflagaAndHodeshStayInStampOrder()
		{
			var events = Culture.Run(Culture.Production, () => Engines.Candidate.CommitSingle(Period("2033-05-01T00:00:00", "2033-05-07T00:00:00"), [46], false));

			Assert.Equal(events.OrderBy(el => At(el.Stamp[..19])).Select(el => el.Stamp), events.Select(el => el.Stamp));
		}
	}
}

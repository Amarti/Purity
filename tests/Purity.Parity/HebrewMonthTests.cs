extern alias Candidate;
using System.Globalization;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>The months a Hebrew date picker offers, on every platform. Dates are made up.</summary>
	public class HebrewMonthTests
	{
		private static IEnumerable<Core.HebrewMonth> EveryMonth()
		{
			for (var month = Core.HebrewMonth.First; month != null; month = month.Next)
				yield return month;
		}


		[Fact]
		public void TheMonthsRunWithoutAGapFromFirstToLast()
		{
			Core.HebrewMonth? previous = null;
			foreach (var month in EveryMonth())
			{
				if (previous != null)
				{
					Assert.Equal(previous, month.Previous);
					Assert.Equal(previous.Daytime(previous.DayCount).AddDays(1), month.Daytime(1));
				}
				Assert.InRange(month.DayCount, 29, 30);
				previous = month;
			}

			Assert.Null(Core.HebrewMonth.First.Previous);
			Assert.Equal(Core.HebrewMonth.Last, previous);
			Assert.Equal(new HebrewCalendar().MaxSupportedDateTime.Date, Core.HebrewMonth.Last.Daytime(Core.HebrewMonth.Last.DayCount));
		}

		[Fact]
		public void EveryDayIsTheDateItSays()
		{
			foreach (var month in EveryMonth())
				for (var day = 1; day <= month.DayCount; day++)
				{
					var daytime = month.Daytime(day);
					var stamp = Core.Stamps.FromDate(daytime, false);

					Assert.Equal(new Core.HebrewOnah(month.Year, month.Month, day, false), Core.Stamps.ToHebrew(stamp));
					Assert.Equal(month, Core.HebrewMonth.Of(daytime));
					Assert.Equal($"{Core.DateText.HebrewDayName(day)} {month.Name}", Core.DateText.Hebrew(stamp));
				}
		}

		[Theory]
		[InlineData(5794, 5, "שבט")]
		[InlineData(5794, 6, "אדר")]
		[InlineData(5794, 7, "ניסן")]
		[InlineData(5795, 6, "אדר א׳")]
		[InlineData(5795, 7, "אדר ב׳")]
		[InlineData(5795, 8, "ניסן")]
		public void MonthsAreNamedByTheirYear(int year, int month, string name) =>
			Assert.Equal(name, new Core.HebrewMonth(year, month).Name);

		[Theory]
		[InlineData(5794, "תשצ\"ד")]
		[InlineData(5765, "תשס\"ה")]
		[InlineData(5783, "תשפ\"ג")]
		[InlineData(5715, "תשט\"ו")]
		[InlineData(5716, "תשט\"ז")]
		[InlineData(5800, "ת\"ת")]
		[InlineData(5400, "ת'")]
		[InlineData(5343, "שמ\"ג")]
		[InlineData(5999, "תתקצ\"ט")]
		public void YearsAreWrittenInLetters(int year, string letters) =>
			Assert.Equal(letters, Core.DateText.HebrewYearName(year));

		[Fact]
		public void AMonthsTitleHasItsYear() => Assert.Equal("שבט תשצ\"ד", new Core.HebrewMonth(5794, 5).Title);

		[Theory]
		[InlineData(5794, 5, 5795, 5)]		// Shevat
		[InlineData(5794, 6, 5795, 7)]		// Adar to Adar II
		[InlineData(5794, 7, 5795, 8)]		// Nisan
		[InlineData(5795, 6, 5794, 6)]		// Adar I to Adar
		[InlineData(5795, 7, 5794, 6)]		// Adar II to Adar
		[InlineData(5795, 8, 5794, 7)]		// Nisan
		public void AMonthKeepsItsPlaceInAnotherYear(int year, int month, int toYear, int toMonth) =>
			Assert.Equal(new Core.HebrewMonth(toYear, toMonth), new Core.HebrewMonth(year, month).InYear(toYear));

		[Fact]
		public void YearsStopAtTheEndsOfTheCalendar()
		{
			Assert.Null(Core.HebrewMonth.Last.InYear(6000));
			Assert.Null(Core.HebrewMonth.First.InYear(5342));
			Assert.Null(new Core.HebrewMonth(5344, 2).InYear(5343));	// Cheshvan 5343 is before the first month
		}

		[Fact]
		public void ADayBeforeTheFirstMonthOpensTheFirstMonth() =>
			Assert.Equal(Core.HebrewMonth.First, Core.HebrewMonth.Of(new DateTime(1583, 1, 1)));

		[Theory]
		[InlineData("2034-02-03T12:00:00", "2034-02-04")]
		[InlineData("2034-02-04T00:00:00", "2034-02-04")]
		[InlineData("2034-02-04T12:00:00", "2034-02-05")]
		public void AStampBelongsToTheDaytimeOfItsHebrewDate(string stamp, string daytime)
		{
			var at = DateTimeOffset.Parse(stamp + "+00:00", CultureInfo.InvariantCulture);

			Assert.Equal(DateTime.Parse(daytime, CultureInfo.InvariantCulture), Core.Stamps.HebrewDaytime(at));
			Assert.Equal(at, Core.Stamps.FromHebrewDaytime(Core.Stamps.HebrewDaytime(at), Core.PurityEvent.IsDateAfterDusk(at)));
		}

		[Fact]
		public void AStampKnowsItsHebrewDateWithTheYear()
		{
			var nightOfTheFifteenth = new DateTimeOffset(2034, 2, 3, 12, 0, 0, TimeSpan.Zero);

			Assert.Equal("ט\"ו שבט תשצ\"ד", Core.DateText.HebrewOfOnahWithYear(nightOfTheFifteenth));
		}

		[Theory]
		[InlineData(1583, 1, 1, true)]
		[InlineData(2239, 9, 28, true)]
		[InlineData(1582, 12, 31, false)]
		[InlineData(2239, 9, 29, false)]
		[InlineData(1, 1, 1, false)]
		public void OnlyDatesTheCalendarReachesCount(int year, int month, int day, bool reached) =>
			Assert.Equal(reached, Core.Stamps.IsInHebrewCalendar(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero)));
	}
}

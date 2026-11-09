extern alias Candidate;
using System.Globalization;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>
	/// Every onah is one stored stamp, readable in either calendar. A Gregorian date runs day then
	/// night and a Hebrew date runs night then day, so a night pairs with the next day's Hebrew date.
	/// Dates are made up.
	/// </summary>
	public class HebrewOnahTests
	{
		private static readonly HebrewCalendar Calendar = new();

		private static DateTimeOffset At(string stamp) => DateTimeOffset.Parse(stamp + "+00:00", CultureInfo.InvariantCulture);

		private static IEnumerable<(DateTime Day, bool Night, DateTimeOffset Stamp)> EveryOnah()
		{
			for (var day = new DateTime(1900, 1, 2); day.Year < 2100; day = day.AddDays(1))
				foreach (var night in new[] { false, true })
					yield return (day, night, Core.Stamps.FromDate(day, night));
		}


		[Theory]
		[InlineData("2034-02-03T00:00:00", 5794, 5, 14, false)]
		[InlineData("2034-02-03T12:00:00", 5794, 5, 15, true)]
		[InlineData("2034-02-04T00:00:00", 5794, 5, 15, false)]
		[InlineData("2034-09-13T12:00:00", 5795, 1, 1, true)]	// the night that opens a Hebrew year
		[InlineData("2034-09-14T00:00:00", 5795, 1, 1, false)]
		[InlineData("2035-03-11T12:00:00", 5795, 7, 1, true)]	// 1 Adar II
		[InlineData("2033-02-28T12:00:00", 5793, 6, 30, true)]	// 30 Adar I, on the last civil night of February
		[InlineData("2033-12-31T12:00:00", 5794, 4, 10, true)]	// 10 Tevet, on the last civil night of a year
		public void AStampIsItsHebrewDateAndOnah(string stamp, int year, int month, int day, bool night)
		{
			var onah = new Core.HebrewOnah(year, month, day, night);

			Assert.Equal(onah, Core.Stamps.ToHebrew(At(stamp)));
			Assert.Equal(At(stamp), Core.Stamps.FromHebrew(onah));
		}

		[Fact]
		public void EveryOnahSurvivesTheTrip()
		{
			foreach (var (_, _, stamp) in EveryOnah())
				Assert.Equal(stamp, Core.Stamps.FromHebrew(Core.Stamps.ToHebrew(stamp)));
		}

		/// <summary>The conversion and the Hebrew text the date rows show must never disagree</summary>
		[Fact]
		public void TheConversionNamesTheDateTheEngineShows()
		{
			foreach (var (_, night, stamp) in EveryOnah())
			{
				var onah = Core.Stamps.ToHebrew(stamp);
				var daytime = new DateTimeOffset(Calendar.ToDateTime(onah.Year, onah.Month, onah.Day, 0, 0, 0, 0), TimeSpan.Zero);
				var shown = new Core.PurityEvent(Core.PurityEventType.Mikveh, stamp);

				Assert.Equal(night, onah.IsNight);
				Assert.Equal(Core.DateText.Hebrew(daytime), shown.StampHebRepr);
				Assert.Equal(shown.StampHebRepr, Core.DateText.HebrewOfOnah(stamp));
			}
		}

		/// <summary>The same switch moves opposite ways: a Gregorian night comes after its day, a Hebrew night before it</summary>
		[Fact]
		public void NightIsAfterTheDayOnAGregorianDateAndBeforeItOnAHebrewOne()
		{
			var twelveHours = TimeSpan.FromHours(12);
			foreach (var (day, night, stamp) in EveryOnah())
			{
				if (night)
					continue;

				var onah = Core.Stamps.ToHebrew(stamp);
				Assert.Equal(twelveHours, Core.Stamps.FromDate(day, true) - stamp);
				Assert.Equal(twelveHours, stamp - Core.Stamps.FromHebrew(onah with { IsNight = true }));
			}
		}

		[Fact]
		public void ADateTheHebrewCalendarDoesntHaveIsRefused() =>
			Assert.Throws<ArgumentOutOfRangeException>(() => Core.Stamps.FromHebrew(new Core.HebrewOnah(5794, 13, 1, false)));	// not a leap year
	}
}

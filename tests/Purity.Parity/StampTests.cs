extern alias Candidate;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>The boundary where the old +03:00 bug entered: a picked date must keep the date the user saw.</summary>
	public class StampTests
	{
		[Theory]
		[InlineData(DateTimeKind.Unspecified)]
		[InlineData(DateTimeKind.Local)]
		[InlineData(DateTimeKind.Utc)]
		public void PlainDateKeepsTheVisibleDate(DateTimeKind kind)
		{
			var stamp = Core.Stamps.FromDate(new DateTime(2033, 4, 17, 0, 0, 0, kind), false);

			Assert.Equal("2033-04-17T00:00:00.0000000+00:00", stamp.ToString("o"));
		}

		[Theory]
		[InlineData(3)]
		[InlineData(-11)]
		[InlineData(14)]
		public void DateWithAnOffsetKeepsTheVisibleDate(int offsetHours)
		{
			var picked = new DateTimeOffset(2033, 4, 17, 0, 0, 0, TimeSpan.FromHours(offsetHours));

			var stamp = Core.Stamps.FromDate(picked, false);

			Assert.Equal("2033-04-17T00:00:00.0000000+00:00", stamp.ToString("o"));
		}

		[Fact]
		public void NightStampIsHourTwelve()
		{
			var stamp = Core.Stamps.FromDate(new DateTime(2033, 4, 17, 15, 30, 0), true);

			Assert.Equal("2033-04-17T12:00:00.0000000+00:00", stamp.ToString("o"));
			Assert.True(Core.PurityEvent.IsDateAfterDusk(stamp));
		}

		/// <summary>The bug itself: an offset stamp shifts the Hebrew month calculation by a day.</summary>
		[Fact]
		public void NormalizedStampIsNotShiftedByTheHebrewMonthCalculation()
		{
			var local = new DateTimeOffset(2033, 4, 17, 0, 0, 0, TimeSpan.FromHours(3));
			var period = new Core.PurityPeriod(Core.Stamps.FromDate(local, false), Core.Stamps.FromDate(local.AddDays(6), false));

			period.Commit([]);

			var hodesh = period.SubEvents.Single(el => el.Type == Core.PurityEventType.VesetHodesh);
			var nextHebrewMonth = new System.Globalization.HebrewCalendar().AddMonths(new DateTime(2033, 4, 17), 1);
			Assert.Equal(new DateTimeOffset(nextHebrewMonth, TimeSpan.Zero), hodesh.Stamp);
		}
	}
}

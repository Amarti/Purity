using Xunit;


namespace Purity.Reminders.Tests
{
	/// <summary>Which days are Yom Tov. These are the public calendar's dates, not anyone's history.</summary>
	public sealed class YomTovTests
	{
		[Theory]
		// 5784 was a leap year, with two Adars before Nisan; 5785 and 5786 weren't
		[InlineData("2024-04-23", true, true)]			// Pesach, first day
		[InlineData("2024-04-24", false, true)]			// its second day, outside Israel only
		[InlineData("2024-04-29", true, true)]			// seventh day of Pesach
		[InlineData("2024-04-30", false, true)]			// eighth day
		[InlineData("2024-06-12", true, true)]			// Shavuot
		[InlineData("2024-06-13", false, true)]
		[InlineData("2024-10-03", true, true)]			// Rosh Hashana, both days everywhere
		[InlineData("2024-10-04", true, true)]
		[InlineData("2024-10-12", true, true)]			// Yom Kippur
		[InlineData("2024-10-17", true, true)]			// Sukkot, first day
		[InlineData("2024-10-18", false, true)]
		[InlineData("2024-10-24", true, true)]			// Shemini Atzeret
		[InlineData("2024-10-25", false, true)]			// Simchat Torah outside Israel
		[InlineData("2026-04-02", true, true)]			// Pesach in an ordinary year
		[InlineData("2026-04-03", false, true)]
		[InlineData("2026-05-22", true, true)]			// Shavuot in an ordinary year
		public void YomTovFallsWhereTheCalendarPutsIt(string date, bool inIsrael, bool outside)
		{
			Assert.Equal(inIsrael, YomTov.IsYomTov(DateTime.Parse(date), inIsrael: true));
			Assert.Equal(outside, YomTov.IsYomTov(DateTime.Parse(date), inIsrael: false));
		}

		[Theory]
		[InlineData("2024-04-22")]						// the eve of Pesach
		[InlineData("2024-04-25")]						// Chol HaMoed
		[InlineData("2024-03-24")]						// Purim
		[InlineData("2024-12-26")]						// Chanukah
		[InlineData("2024-08-13")]						// Tisha B'Av
		[InlineData("2024-10-11")]						// the eve of Yom Kippur
		[InlineData("2024-10-23")]						// Hoshana Rabba
		public void DaysWhenWorkIsAllowedArentYomTov(string date)
		{
			Assert.False(YomTov.IsYomTov(DateTime.Parse(date), inIsrael: true));
			Assert.False(YomTov.IsYomTov(DateTime.Parse(date), inIsrael: false));
		}

		[Fact]
		public void DatesBeyondTheHebrewCalendarArentYomTov()
		{
			Assert.False(YomTov.IsYomTov(DateTime.MinValue, true));
			Assert.False(YomTov.IsYomTov(new DateTime(2300, 4, 1), false));
		}

		[Fact]
		public void EveryShabbatIsARestDay()
		{
			for (var date = new DateTime(2031, 1, 4); date.Year == 2031; date = date.AddDays(7))
				Assert.True(YomTov.IsRestDay(date, true));
			Assert.False(YomTov.IsRestDay(new DateTime(2031, 1, 5), false));		// an ordinary Sunday
		}

		[Theory]
		[InlineData("Asia/Jerusalem", true)]
		[InlineData("Asia/Tel_Aviv", true)]
		[InlineData("Israel Standard Time", true)]		// as Windows names it
		[InlineData("UTC", false)]
		[InlineData("America/New_York", false)]
		[InlineData("Eastern Standard Time", false)]
		[InlineData("Asia/Hebron", false)]
		public void IsraelsTimeZoneIsKnownByEitherName(string id, bool israel) =>
			Assert.Equal(israel, YomTov.IsIsraelZone(TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(2), id, id)));
	}
}

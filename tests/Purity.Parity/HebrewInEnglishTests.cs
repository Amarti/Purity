extern alias Candidate;
using System.Globalization;
using Xunit;
using Core = Candidate::Purity.Core;


namespace Purity.Parity
{
	/// <summary>Hebrew dates written in English letters, the option for every place a Hebrew date shows. Dates are made up.</summary>
	public class HebrewInEnglishTests
	{
		[Theory]
		[InlineData("2034-02-03T00:00:00", "14 Shevat", "14 Shevat 5794")]
		[InlineData("2034-02-03T12:00:00", "15 Shevat", "15 Shevat 5794")]		// a night belongs to the date it opens
		[InlineData("2034-02-04T00:00:00", "15 Shevat", "15 Shevat 5794")]
		[InlineData("2034-09-13T12:00:00", "1 Tishrei", "1 Tishrei 5795")]
		[InlineData("2035-03-11T12:00:00", "1 Adar II", "1 Adar II 5795")]
		[InlineData("2033-02-28T12:00:00", "30 Adar I", "30 Adar I 5793")]
		public void AStampIsWrittenInEnglishLetters(string stamp, string date, string withYear)
		{
			var at = DateTimeOffset.Parse(stamp + "+00:00", CultureInfo.InvariantCulture);

			Assert.Equal(date, Core.DateText.HebrewOfOnahInEnglish(at));
			Assert.Equal(withYear, Core.DateText.HebrewOfOnahInEnglishWithYear(at));
		}

		[Theory]
		[InlineData(5794, 2, "Cheshvan", "Cheshvan 5794")]
		[InlineData(5794, 6, "Adar", "Adar 5794")]
		[InlineData(5794, 7, "Nisan", "Nisan 5794")]
		[InlineData(5795, 6, "Adar I", "Adar I 5795")]
		[InlineData(5795, 7, "Adar II", "Adar II 5795")]
		[InlineData(5795, 8, "Nisan", "Nisan 5795")]
		public void MonthsAreNamedByTheirYear(int year, int month, string name, string title)
		{
			var hebrewMonth = new Core.HebrewMonth(year, month);

			Assert.Equal(name, hebrewMonth.EnglishName);
			Assert.Equal(title, hebrewMonth.EnglishTitle);
		}
	}
}

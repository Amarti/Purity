using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Purity.Desktop.Localization;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// The weekday row of the month grid. In a Hebrew month both it and the dates are letters, so they
	/// must look different: the row is bold on a band, and the dates keep the day buttons' own type.
	/// </summary>
	public sealed partial class LookTests
	{
		[Theory]
		[InlineData(CalendarKind.Hebrew)]
		[InlineData(CalendarKind.Gregorian)]
		public Task TheWeekdayRowLooksUnlikeTheDatesUnderIt(CalendarKind calendar) => Ui.Run(() =>
		{
			var (window, month) = ShowMonth(new DateTime(2030, 9, 4), calendar: calendar);
			var weekdays = month.GetVisualDescendants().OfType<TextBlock>().Where(el => el.Classes.Contains("Weekday")).ToArray();
			var dates = DaysOf(month).SelectMany(el => el.GetVisualDescendants().OfType<TextBlock>()).ToArray();

			Assert.Equal(7, weekdays.Length);
			Assert.NotEmpty(dates);
			Assert.All(weekdays, el => Assert.Equal(FontWeight.Bold, el.FontWeight));
			Assert.All(dates, el => Assert.Equal(FontWeight.Normal, el.FontWeight));
			Assert.All(dates, el => Assert.Equal(11, el.FontSize));
			Assert.Contains(month.GetVisualDescendants().OfType<Border>(), el => Grid.GetRow(el) == 0 && Grid.GetColumnSpan(el) == 7 && el.Background != null);

			window.Close();
		});
	}
}

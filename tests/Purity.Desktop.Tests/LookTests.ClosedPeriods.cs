using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Changing a date of a closed period asks first, and a change not confirmed leaves no trace on screen</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task APickedDateWaitsForTheAnswerAndGoesBackIfCancelled() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var period = ((MainWindowViewModel)window.DataContext!).Periods[0];
			Assert.True(period.IsClosed);
			var field = window.GetVisualDescendants().OfType<DateField>().Single(el => el.DataContext == period && AutomationProperties.GetName(el) == UiText.English.To);
			var layer = window.FindControl<MessageLayer>("Messages")!;
			var (shown, day) = (field.Text, field.SelectedDay!.Value);

			field.SetCurrentValue(DateField.SelectedDayProperty, day.AddDays(2));		// as picking a day in its month does
			Dispatcher.UIThread.RunJobs();
			Assert.True(layer.IsAsking);
			Assert.Equal(shown, field.Text);

			AnswerWith(UiText.English.Cancel, layer);
			Assert.Equal(day, field.SelectedDay);
			Assert.Equal(shown, field.Text);

			field.SetCurrentValue(DateField.SelectedDayProperty, day.AddDays(2));
			Dispatcher.UIThread.RunJobs();
			AnswerWith(UiText.English.Change, layer);
			Assert.Equal(day.AddDays(2), field.SelectedDay);
			Assert.Equal(period.EndText, field.Text);
			Assert.NotEqual(shown, field.Text);
			window.Close();
		});

		[Fact]
		public Task APressedOnahWaitsForTheAnswerAndGoesBackIfCancelled() => Ui.Run(async () =>
		{
			var window = await OpenPeriodsAsync();
			var period = ((MainWindowViewModel)window.DataContext!).Periods[0];
			var (_, end) = SwitchesOf(window, period);
			var (day, night) = Halves(end);
			var layer = window.FindControl<MessageLayer>("Messages")!;

			Press(night, window);
			Assert.True(layer.IsAsking);
			Assert.Equal(new bool?[] { true, false }, new[] { day.IsChecked, night.IsChecked });

			AnswerWith(UiText.English.Cancel, layer);
			Assert.False(period.EndIsAfterDusk);
			Assert.Equal(new bool?[] { true, false }, new[] { day.IsChecked, night.IsChecked });

			Press(night, window);
			AnswerWith(UiText.English.Change, layer);
			Assert.True(period.EndIsAfterDusk);
			Assert.Equal(new bool?[] { false, true }, new[] { day.IsChecked, night.IsChecked });
			window.Close();
		});


		/// <summary>Says Change to the question a closed period's new date brings up, in the language on show</summary>
		private static void ConfirmChange(Window window) => AnswerWith(Loc.T.Change, window.FindControl<MessageLayer>("Messages")!);
	}
}

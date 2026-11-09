using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The other calendar's date, on real windows: in the tooltips of the date fields and of the folded lines</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task TheDateFieldsTooltipsFollowTheSwitch() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var first = ((MainWindowViewModel)window.DataContext!).Periods[0];
			var fields = window.GetVisualDescendants().OfType<DateField>().Where(el => el.DataContext == first).ToArray();
			Assert.Equal(2, fields.Length);
			Assert.Equal(first.BeginInOtherCalendar, ToolTip.GetTip(fields[0]));
			Assert.Equal(first.EndInOtherCalendar, ToolTip.GetTip(fields[1]));
			var before = (string)ToolTip.GetTip(fields[0])!;
			Assert.EndsWith(", day", before);

			Press(Halves(SwitchesOf(window, first).Begin).Night, window);

			var after = (string)ToolTip.GetTip(fields[0])!;
			Assert.EndsWith(", night", after);
			Assert.NotEqual(before.Split(',')[0], after.Split(',')[0]);		// a night belongs to the next Hebrew date
			Assert.Equal(first.BeginInOtherCalendar, after);
			window.Close();
		});

		[Fact]
		public Task AFoldedLineHasTheSameTooltips() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var folded = ((MainWindowViewModel)window.DataContext!).Periods[1];
			Assert.True(folded.IsFolded);

			await Ui.WaitUntil(() => TipsOf(window, folded).Length == 2);

			Assert.Equal(new object?[] { folded.BeginInOtherCalendar, folded.EndInOtherCalendar }, TipsOf(window, folded));
			window.Close();
		});


		/// <summary>A tooltip on a control with parts of its own has to open over those parts too</summary>
		[Fact]
		public Task HoveringADateOpensItsTooltip() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var model = (MainWindowViewModel)window.DataContext!;
			await Ui.WaitUntil(() => TipsOf(window, model.Periods[1]).Length == 2);
			var field = window.GetVisualDescendants().OfType<DateField>().First(el => el.DataContext == model.Periods[0]);
			var folded = window.GetVisualDescendants().OfType<StackPanel>().First(el => el.DataContext == model.Periods[1] && ToolTip.GetTip(el) != null);

			Hover(field, window);
			await Ui.WaitUntil(() => ToolTip.GetIsOpen(field));

			Hover(folded, window);
			await Ui.WaitUntil(() => ToolTip.GetIsOpen(folded));
			window.Close();
		});


		private static void Hover(Control control, Window window)
		{
			// The pointer finds what was last drawn, so a frame has to be drawn first
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			Dispatcher.UIThread.RunJobs();

			window.MouseMove(control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value);
			Dispatcher.UIThread.RunJobs();
		}

		private static object?[] TipsOf(Window window, PeriodViewModel period) =>
			window.GetVisualDescendants().OfType<StackPanel>().Where(el => el.DataContext == period && ToolTip.GetTip(el) != null).Select(ToolTip.GetTip).ToArray();
	}
}

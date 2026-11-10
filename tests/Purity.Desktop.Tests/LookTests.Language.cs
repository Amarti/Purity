using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The language switch on real windows: pressing a flag changes what they show at once</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task PressingTheFlagChangesBothWindowsAtOnce() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var englishDate = window.GetVisualDescendants().OfType<DateField>().First().Text;
			Assert.Equal(UiText.English.Settings, settings.Title);
			Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);

			Press(Flag(settings, "עברית"), settings);
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(UiText.Hebrew.Settings, settings.Title);
			Assert.Equal(FlowDirection.RightToLeft, settings.FlowDirection);
			Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
			Assert.Contains(settings.GetVisualDescendants().OfType<Button>(), el => el.Content as string == UiText.Hebrew.Ok);
			Assert.Equal(UiText.Hebrew.Light, ToolTip.GetTip(AppearancePartsOf(settings)[1]));
			Assert.True(AppearancePartsOf(settings)[0].IsChecked);

			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<DateField>().Any(el => el.Bounds.Width > 0));
			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<DateField>().FirstOrDefault(el => el.Bounds.Width > 0)?.Text is { } text && text != englishDate);
			var field = window.GetVisualDescendants().OfType<DateField>().First(el => el.Bounds.Width > 0);
			Assert.True(field.Text!.StartsWith("יום") || field.Text.StartsWith("שבת"), "The date field should show the date in Hebrew");
			Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == UiText.Hebrew.Mikveh);

			settings.Close(true);
			await closed;
			Assert.Equal(Language.Hebrew, DesktopSettings.Load().Language);
			window.Close();
		});

		[Fact]
		public Task LeavingWithoutOkBringsTheDatesBack() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var englishDate = window.GetVisualDescendants().OfType<DateField>().First().Text;

			Press(Flag(settings, "עברית"), settings);
			Dispatcher.UIThread.RunJobs();
			settings.Close(false);
			await closed;

			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<DateField>().Any(el => el.Bounds.Width > 0));
			await Ui.WaitUntil(() => window.GetVisualDescendants().OfType<DateField>().First().Text == englishDate);
			Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
			Assert.Equal(Language.English, DesktopSettings.Load().Language);
			window.Close();
		});

		/// <summary>The window comes up already in the language that was saved</summary>
		[Fact]
		public Task TheSavedLanguageIsShownFromTheStart() => BackToEnglishAfter(async () =>
		{
			new DesktopSettings { Language = Language.Hebrew }.Save();

			var window = await OpenPeriodsAsync();

			Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
			Assert.True(window.GetVisualDescendants().OfType<DateField>().First().Text!.Contains('ב'), "The date field should be in Hebrew");
			Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == UiText.Hebrew.Next);
			window.Close();
		});

		[Fact]
		public Task TheFlagsDontSwapPlacesInHebrew() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var before = Flag(settings, "English").TranslatePoint(default, settings)!.Value;

			Press(Flag(settings, "עברית"), settings);
			Dispatcher.UIThread.RunJobs();
			await Ui.WaitUntil(() => settings.FlowDirection == FlowDirection.RightToLeft);

			Assert.Equal(FlowDirection.LeftToRight, Flag(settings, "English").FlowDirection);
			Assert.Equal(before.Y, Flag(settings, "English").TranslatePoint(default, settings)!.Value.Y);
			settings.Close(false);
			await closed;
			window.Close();
		});


		private static RadioButton Flag(Window settings, string name) =>
			settings.GetVisualDescendants().OfType<RadioButton>().Single(el => AutomationProperties.GetName(el) == name);

		private static async Task<(SettingsWindow Settings, Task Closed)> OpenSettingsAsync(MainWindow window)
		{
			var closed = ((MainWindowViewModel)window.DataContext!).OpenSettingsCommand.ExecuteAsync(null);
			await Ui.WaitUntil(() => window.OwnedWindows.Count > 0);
			var settings = (SettingsWindow)window.OwnedWindows[0];
			await Ui.WaitUntil(() => settings.GetVisualDescendants().OfType<RadioButton>().Any(el => el.Bounds.Width > 0));
			return (settings, closed);
		}

		private static Task BackToEnglishAfter(Func<Task> test) => Ui.Run(async () =>
		{
			try
			{
				await test();
			}
			finally
			{
				Loc.Instance.Use(Language.English);
				Loc.Instance.UseCalendar(CalendarKind.Gregorian, false);
			}
		});
	}
}

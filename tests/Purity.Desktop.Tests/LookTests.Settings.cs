using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The look of the settings window</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task TheAppearanceSwitchIsPicturesThatSayWhatTheyAreInTooltips() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var parts = AppearancePartsOf(settings);

			Assert.Equal(3, parts.Length);
			Assert.All(parts, el => Assert.IsType<Image>(el.Content));
			Assert.All(parts, el => Assert.Empty(el.GetVisualDescendants().OfType<TextBlock>()));
			Assert.Equal(new object?[] { UiText.English.SameAsSystem, UiText.English.Light, UiText.English.Dark }, parts.Select(el => ToolTip.GetTip(el)));

			settings.Close(false);
			await closed;
			window.Close();
		});

		[Fact]
		public Task RemovingAllHistoryIsARedButtonAfterTheFileButtons() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var buttons = settings.GetVisualDescendants().OfType<Button>().ToList();
			Button Labelled(string text) => buttons.Single(el => el.Content as string == text);
			var wipe = Labelled(UiText.English.WipeOut);

			Assert.Contains("Danger", wipe.Classes);
			Assert.True(wipe.IsEnabled);
			Assert.Equal(UiText.English.WipeOutTip, ToolTip.GetTip(wipe));
			Assert.True(Grid.GetRow(wipe) > Grid.GetRow(Labelled(UiText.English.MoveFile)));
			Assert.True(Grid.GetRow(wipe) < Grid.GetRow(Labelled(UiText.English.CreateReport)));

			settings.Close(false);
			await closed;
			window.Close();
		});

		/// <summary>A report that couldn't be written is said over this window, not over the one behind it</summary>
		[Fact]
		public Task TheSettingsWindowSpeaksThroughItsOwnLayer() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var layer = settings.FindControl<MessageLayer>("Messages")!;

			layer.Tell("The report couldn't be written", MessageKind.Problem);
			Dispatcher.UIThread.RunJobs();

			Assert.Single(PillsOf(layer));
			Assert.Same(settings, TopLevel.GetTopLevel(layer));
			settings.Close(false);
			await closed;
			window.Close();
		});

		[Fact]
		public Task TheConfirmingButtonStandsOut() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var (settings, closed) = await OpenSettingsAsync(window);
			var buttons = settings.GetVisualDescendants().OfType<Button>().ToArray();
			var ok = buttons.Single(el => el.Content as string == UiText.English.Ok);
			var cancel = buttons.Single(el => el.Content as string == UiText.English.Cancel);

			Assert.Contains("Confirm", ok.Classes);
			Assert.DoesNotContain("Confirm", cancel.Classes);
			Assert.Equal(Color.Parse("#44719A"), ColorOf(ok.GetVisualDescendants().OfType<ContentPresenter>().First().Background));
			Assert.NotEqual(Color.Parse("#44719A"), ColorOf(cancel.GetVisualDescendants().OfType<ContentPresenter>().First().Background));

			settings.Close(false);
			await closed;
			window.Close();
		});

		private static RadioButton[] AppearancePartsOf(Window settings) =>
			settings.GetVisualDescendants().OfType<RadioButton>().Where(el => el.GroupName == "Appearance").ToArray();
	}
}

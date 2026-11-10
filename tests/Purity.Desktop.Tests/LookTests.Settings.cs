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
	}
}

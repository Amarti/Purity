using Avalonia.Platform;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>The font is built into the app, so it can't depend on what a computer has installed</summary>
	public sealed partial class LookTests
	{
		[Theory]
		[InlineData("Heebo-Light.ttf")]
		[InlineData("Heebo-Regular.ttf")]
		[InlineData("Heebo-Medium.ttf")]
		[InlineData("Heebo-SemiBold.ttf")]
		[InlineData("Heebo-Bold.ttf")]
		[InlineData("OFL.txt")]
		public Task TheFontTravelsWithTheApp(string file) => Ui.Run(() =>
			Assert.True(AssetLoader.Exists(new Uri($"avares://Purity/Assets/Fonts/{file}")), $"{file} isn't built in"));

		[Fact]
		public Task WindowsUseHeebo() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();

			Assert.Equal("Heebo", window.FontFamily.Name);
			window.Close();
		});
	}
}

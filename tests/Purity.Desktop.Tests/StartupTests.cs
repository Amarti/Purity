using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	public sealed class StartupTests
	{
		[Fact]
		public void AWindowThatFitsKeepsItsSize() => Assert.Equal(861, WindowFit.Height(861, 32, 1040));

		[Fact]
		public void OnA15InchMacBookProTheWindowGetsShorter()
		{
			// 900 points, less the menu bar (24) and a Dock at the bottom (70); the title bar adds 28
			Assert.Equal(778, WindowFit.Height(861, 28, 900 - 24 - 70));
		}

		[Fact]
		public void TheWindowNeverGetsTooShortToUse() => Assert.Equal(WindowFit.MinimumHeight, WindowFit.Height(861, 28, 300));

		[Fact]
		public void CommandLineOptionsAreRead()
		{
			Assert.Equal(new StartOptions("a.json", "b.png"), StartOptions.Parse(new[] { "--data", "a.json", "--picture", "b.png" }));
			Assert.Equal(new StartOptions(null), StartOptions.Parse(new[] { "-psn_0_12345" }));
			Assert.Equal(new StartOptions(null), StartOptions.Parse(new[] { "--data" }));
		}
	}
}

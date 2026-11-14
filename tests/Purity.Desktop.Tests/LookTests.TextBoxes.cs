using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.UI.Localization;
using Purity.UI.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>
	/// Text boxes holding text in both directions. In Hebrew, Avalonia 12.1.3 crashed when such a box
	/// was clicked: on a Mac the data file path was enough, since its leading slash runs right to left.
	/// </summary>
	public sealed partial class LookTests
	{
		[Theory]
		[InlineData("/Users/someone/Dropbox/pureData.json")]
		[InlineData(@"C:\Users\אלי\Dropbox\pureData.json")]
		[InlineData("טהרה Purity")]
		[InlineData("טהרה 2026")]
		public Task ARightToLeftBoxWithTextInBothDirectionsCanBeClickedAndSelected(string text) => Ui.Run(() =>
		{
			var box = new TextBox { Text = text, Width = 214, Height = 23, FlowDirection = FlowDirection.RightToLeft };
			var window = new Window { Content = box, Width = 300, Height = 80 };
			window.Show();
			Drawn();

			for (var x = 1.0; x < box.Bounds.Width; x += 6)
			{
				var at = box.TranslatePoint(new Point(x, box.Bounds.Height / 2), window)!.Value;
				window.MouseDown(at, MouseButton.Left);
				window.MouseUp(at, MouseButton.Left);
				Drawn();
			}
			for (var start = 0; start <= text.Length; start++)
			{
				box.CaretIndex = start;
				window.KeyPress(Key.Right, RawInputModifiers.Shift, PhysicalKey.None, null);
				window.KeyPress(Key.Left, RawInputModifiers.Shift, PhysicalKey.None, null);
				window.KeyPress(Key.Left, RawInputModifiers.Shift, PhysicalKey.None, null);
				Drawn();
			}
			box.SelectAll();
			Drawn();

			Assert.Equal(text, box.Text);
			window.Close();
		});

		[Fact]
		public Task TheDataFilePathReadsLeftToRightInHebrew() => BackToEnglishAfter(async () =>
		{
			Loc.Instance.Use(Language.Hebrew);
			var settings = new SettingsWindowViewModel("/Users/someone/Dropbox/pureData.json", null);
			var window = DialogWindow.For(new SettingsView(settings));
			window.Show();
			Drawn();
			var box = window.FindNamed<TextBox>("DataFileBox")!;
			Assert.Equal(settings.DataFilePath, box.Text);

			Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
			Assert.Equal(FlowDirection.LeftToRight, box.FlowDirection);
			Press(box, window);
			Assert.Equal("/Users/someone/Dropbox/pureData.json", settings.DataFilePath);

			window.Close();
			await Task.CompletedTask;
		});


		/// <summary>The pointer finds what was last drawn, so each step draws a frame</summary>
		private static void Drawn()
		{
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessPlatform.ForceRenderTimerTick();
			Dispatcher.UIThread.RunJobs();
		}
	}
}

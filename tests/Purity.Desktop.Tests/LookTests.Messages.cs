using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;
using Purity.Desktop.Views;
using Xunit;


namespace Purity.Desktop.Tests
{
	/// <summary>Pills for what only needs saying, and sheets for what needs an answer</summary>
	public sealed partial class LookTests
	{
		[Fact]
		public Task GoodNewsFadesByItself() => Ui.Run(async () =>
		{
			var (window, layer) = ShowLayer();
			layer.DoneFor = TimeSpan.FromMilliseconds(30);

			layer.Tell("Your history is back", MessageKind.Done);

			Assert.Single(PillsOf(layer));
			Assert.Empty(CloseButtonsOf(layer));
			await Ui.WaitUntil(() => PillsOf(layer).Length == 0);
			window.Close();
		});

		[Theory]
		[InlineData(MessageKind.Problem)]
		[InlineData(MessageKind.Critical)]
		public Task TroubleStaysUntilItsClosed(MessageKind kind) => Ui.Run(async () =>
		{
			var (window, layer) = ShowLayer();
			layer.DoneFor = TimeSpan.FromMilliseconds(1);

			layer.Tell("It couldn't be saved", kind);
			await Task.Delay(50);
			Dispatcher.UIThread.RunJobs();

			Assert.Single(PillsOf(layer));
			CloseButtonsOf(layer).Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
			Assert.Empty(PillsOf(layer));
			window.Close();
		});

		[Fact]
		public Task APillKeepsToOneParagraph() => Ui.Run(() =>
		{
			var (window, layer) = ShowLayer();

			layer.Tell("First part.\n\nSecond part.", MessageKind.Problem);

			Assert.Equal("First part. Second part.", PillsOf(layer).Single().GetVisualDescendants().OfType<TextBlock>().Single().Text);
			window.Close();
		});

		[Fact]
		public Task ASheetWaitsForItsAnswer() => Ui.Run(async () =>
		{
			var (window, layer) = ShowLayer();

			var asking = layer.AskAsync(new Question("Remove it?", "It can't be undone."), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();

			Assert.False(asking.IsCompleted);
			Assert.True(layer.IsAsking);
			Assert.Contains(layer.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "Remove it?");
			Assert.Contains(layer.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "It can't be undone.");

			AnswerWith("Yes", layer);

			Assert.Equal(0, await asking);
			Assert.False(layer.IsAsking);
			window.Close();
		});

		[Fact]
		public Task TheWayOutComesFirstAndOnlyTheOtherIsBlue() => Ui.Run(() =>
		{
			var (window, layer) = ShowLayer();

			_ = layer.AskAsync(new Question("Remove it?"), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();

			var buttons = SheetButtonsOf(layer);
			Assert.Equal(new object?[] { "No", "Yes" }, buttons.Select(el => el.Content));
			Assert.DoesNotContain("Confirm", buttons[0].Classes);
			Assert.Contains("Confirm", buttons[1].Classes);
			layer.CancelQuestion();
			window.Close();
		});

		[Fact]
		public Task WithNoPreferenceNoButtonStandsOut() => Ui.Run(() =>
		{
			var (window, layer) = ShowLayer();

			_ = layer.AskAsync(new Question("Which one?", NoPreference: true), new[] { "Keep mine", "Keep theirs" });
			Dispatcher.UIThread.RunJobs();

			var buttons = SheetButtonsOf(layer);
			Assert.Equal(new object?[] { "Keep mine", "Keep theirs" }, buttons.Select(el => el.Content));
			Assert.All(buttons, el => Assert.DoesNotContain("Confirm", el.Classes));
			layer.CancelQuestion();
			window.Close();
		});

		/// <summary>Only as wide as their words, gathered at the start of the line, which a Hebrew window mirrors</summary>
		[Theory]
		[InlineData(FlowDirection.LeftToRight)]
		[InlineData(FlowDirection.RightToLeft)]
		public Task TheButtonsTakeOnlyTheRoomTheyNeed(FlowDirection direction) => Ui.Run(() =>
		{
			var (window, layer) = ShowLayer();
			window.FlowDirection = direction;

			_ = layer.AskAsync(new Question("Remove it?"), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessForceLayout(window);

			var row = SheetButtonsOf(layer)[0].GetVisualParent<StackPanel>()!;
			Assert.Equal(HorizontalAlignment.Left, row.HorizontalAlignment);
			Assert.Equal(Orientation.Horizontal, row.Orientation);
			Assert.True(row.Bounds.Width < layer.Bounds.Width / 2, $"the buttons take {row.Bounds.Width} of {layer.Bounds.Width}");
			layer.CancelQuestion();
			window.Close();
		});

		[Fact]
		public Task AQuestionTooLongForTheWindowKeepsItsAnswersInSight() => Ui.Run(() =>
		{
			var (window, layer) = ShowLayer();
			window.Height = 300;

			_ = layer.AskAsync(new Question("Which one?", string.Join("\n", Enumerable.Repeat("A line of the explanation", 200))), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();
			AvaloniaHeadlessForceLayout(window);

			foreach (var button in SheetButtonsOf(layer))
			{
				var bottom = button.TranslatePoint(new Point(0, button.Bounds.Height), layer)!.Value.Y;
				Assert.InRange(bottom, 0, layer.Bounds.Height);
			}
			var title = layer.GetVisualDescendants().OfType<TextBlock>().Single(el => el.Text == "Which one?");
			Assert.InRange(title.TranslatePoint(new Point(0, 0), layer)!.Value.Y, 0, layer.Bounds.Height);
			layer.CancelQuestion();
			window.Close();
		});

		[Fact]
		public Task TheKeyboardGoesToTheQuestionAndComesBack() => Ui.Run(async () =>
		{
			var before = new Button { Content = "Before" };
			var layer = new MessageLayer();
			var window = new Window { Width = 620, Height = 500, Content = new Panel { Children = { before, layer } } };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			before.Focus();
			Dispatcher.UIThread.RunJobs();
			Assert.True(before.IsFocused);

			var asking = layer.AskAsync(new Question("Remove it?"), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();
			var yes = SheetButtonsOf(layer).Single(el => el.Content as string == "Yes");
			Assert.True(yes.IsFocused);

			AnswerWith("No", layer);
			await asking;
			Dispatcher.UIThread.RunJobs();
			Assert.True(before.IsFocused);
			window.Close();
		});

		[Fact]
		public Task EscapeLeavesTheQuestionUnanswered() => Ui.Run(async () =>
		{
			var (window, layer) = ShowLayer();
			var asking = layer.AskAsync(new Question("Remove it?"), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();

			SheetButtonsOf(layer)[0].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

			Assert.Null(await asking);
			Assert.False(layer.IsAsking);
			window.Close();
		});

		[Fact]
		public Task QuestionsComeOneAtATime() => Ui.Run(async () =>
		{
			var (window, layer) = ShowLayer();

			var first = layer.AskAsync(new Question("First?"), new[] { "Yes", "No" });
			var second = layer.AskAsync(new Question("Second?"), new[] { "Yes", "No" });
			Dispatcher.UIThread.RunJobs();
			Assert.Contains(layer.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "First?");
			Assert.DoesNotContain(layer.GetVisualDescendants().OfType<TextBlock>(), el => el.Text == "Second?");

			AnswerWith("No", layer);
			Assert.Equal(1, await first);
			await Ui.WaitUntil(() => layer.GetVisualDescendants().OfType<TextBlock>().Any(el => el.Text == "Second?"));

			AnswerWith("Yes", layer);
			Assert.Equal(0, await second);
			window.Close();
		});

		[Fact]
		public Task TheMainWindowSpeaksThroughItsLayer() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var layer = window.GetVisualDescendants().OfType<MessageLayer>().Single();

			((IAppDialogs)window).Tell("It couldn't be saved", MessageKind.Problem);
			var asking = ((IAppDialogs)window).AskAsync(new Question("Remove it?"), "Yes", "No");
			Dispatcher.UIThread.RunJobs();

			Assert.Single(PillsOf(layer));
			Assert.True(layer.IsAsking);
			AnswerWith("No", layer);
			Assert.Equal(1, await asking);
			window.Close();
		});

		[Fact]
		public Task ClosingTheWindowLeavesAQuestionUnanswered() => BackToEnglishAfter(async () =>
		{
			var window = await OpenPeriodsAsync();
			var asking = ((IAppDialogs)window).AskAsync(new Question("Remove it?"), "Yes", "No");
			Dispatcher.UIThread.RunJobs();

			window.Close();

			Assert.Null(await asking);
		});

		[Fact]
		public Task EveryButtonIsRounded() => Ui.Run(() =>
		{
			var button = new Button { Content = "Press" };
			var window = new Window { Content = button };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			Assert.Equal(new CornerRadius(17), button.CornerRadius);
			window.Close();
		});


		private static (Window Window, MessageLayer Layer) ShowLayer()
		{
			var layer = new MessageLayer();
			var window = new Window { Width = 620, Height = 500, Content = layer };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return (window, layer);
		}

		private static void AvaloniaHeadlessForceLayout(Window window)
		{
			window.UpdateLayout();
			Dispatcher.UIThread.RunJobs();
		}

		private static Border[] PillsOf(MessageLayer layer) =>
			layer.GetVisualDescendants().OfType<Border>().Where(el => el.Classes.Contains("Pill")).ToArray();

		private static Button[] CloseButtonsOf(MessageLayer layer) =>
			layer.GetVisualDescendants().OfType<Button>().Where(el => el.Classes.Contains("MessageClose")).ToArray();

		private static Button[] SheetButtonsOf(MessageLayer layer) =>
			layer.GetVisualDescendants().OfType<Button>().Where(el => !el.Classes.Contains("MessageClose")).ToArray();

		private static void AnswerWith(string label, MessageLayer layer)
		{
			SheetButtonsOf(layer).Single(el => el.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}
	}
}

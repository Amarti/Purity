using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Purity.Desktop.Localization;
using Purity.Desktop.ViewModels;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// What the app has to say, drawn over its window. Something that only needs saying is a pill at
	/// the bottom, which the person can work around. A question is a sheet rising from the bottom,
	/// and the window waits behind it for an answer.
	/// </summary>
	public sealed class MessageLayer : Panel
	{
		public MessageLayer()
		{
			Children.Add(_pills);
		}


		/// <summary>How long a pill saying that something went well stays</summary>
		public TimeSpan DoneFor { get; set; } = TimeSpan.FromSeconds(6);

		/// <summary>Whether a question is waiting for its answer</summary>
		public bool IsAsking => _sheet != null;

		/// <summary>Shows a pill. Good news fades by itself; anything else stays until it's closed.</summary>
		public void Tell(string text, MessageKind kind)
		{
			var pill = new Border();
			pill.Classes.Add("Pill");
			pill.Child = PillContent(text, kind, pill);
			_pills.Children.Add(pill);

			if (kind == MessageKind.Done)
				DispatcherTimer.RunOnce(() => Fade(pill), DoneFor);
		}

		/// <summary>Shows a question, one at a time, and waits for it</summary>
		/// <param name="question">What's being asked</param>
		/// <param name="buttons">The answers. The first is the one that goes ahead, unless the question has no preference.</param>
		/// <returns>The index of the button pressed, or null when the question was closed without one</returns>
		public async Task<int?> AskAsync(Question question, IReadOnlyList<string> buttons)
		{
			var answer = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
			var before = _lastQuestion;
			_lastQuestion = answer.Task;
			await before;

			ShowSheet(question, buttons, answer);
			return await answer.Task;
		}

		/// <summary>Closes the question on screen without an answer, as when the window is closing</summary>
		public void CancelQuestion() => Answer(null);


		private Grid PillContent(string text, MessageKind kind, Border pill)
		{
			var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
			row.Children.Add(new Image { Classes = { "MessageIcon" }, Source = Icon(kind) });

			var label = new TextBlock { Text = text.Replace("\n\n", " ") };
			Grid.SetColumn(label, 1);
			row.Children.Add(label);

			if (kind != MessageKind.Done)
			{
				var close = new Button { Theme = this.FindResource("PlainButtonTheme") as Avalonia.Styling.ControlTheme, Classes = { "MessageClose" } };
				close.Content = new Image { Width = 14, Height = 14, Source = this.FindResource("MessageCloseIconDrawingImage") as Avalonia.Media.IImage };
				AutomationProperties.SetName(close, Loc.T.Close);
				close.Click += (_, _) => _pills.Children.Remove(pill);
				Grid.SetColumn(close, 2);
				row.Children.Add(close);
			}
			return row;
		}

		private Avalonia.Media.IImage? Icon(MessageKind kind) => this.FindResource(kind switch
		{
			MessageKind.Done => "MessageDoneIconDrawingImage",
			MessageKind.Critical => "MessageCriticalIconDrawingImage",
			_ => "MessageProblemIconDrawingImage",
		}) as Avalonia.Media.IImage;

		private void Fade(Border pill)
		{
			pill.Opacity = 0;
			DispatcherTimer.RunOnce(() => _pills.Children.Remove(pill), FadeTime);
		}

		private void ShowSheet(Question question, IReadOnlyList<string> buttons, TaskCompletionSource<int?> answer)
		{
			var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
			content.Children.Add(new TextBlock { Classes = { "SheetTitle" }, Text = question.Title });
			if (question.Body != null)
			{
				// However long the explanation and however short the window, the answers stay in sight
				var body = new ScrollViewer { Content = new TextBlock { Classes = { "SheetBody" }, Text = question.Body } };
				Grid.SetRow(body, 1);
				content.Children.Add(body);
			}

			var row = new StackPanel { Classes = { "SheetButtons" }, Orientation = Orientation.Horizontal };
			var firstButton = default(Button);
			foreach (var index in ShownOrder(question, buttons.Count))
			{
				var button = new Button { Content = buttons[index] };
				if (index == 0)
				{
					firstButton = button;
					if (!question.NoPreference)
						button.Classes.Add("Confirm");
				}
				button.Click += (_, _) => Answer(index);
				row.Children.Add(button);
			}
			Grid.SetRow(row, 2);
			content.Children.Add(row);

			var sheet = new Border { Classes = { "Sheet" }, Child = content };
			KeyboardNavigation.SetTabNavigation(sheet, KeyboardNavigationMode.Cycle);

			var dim = new Border { Classes = { "MessageDim" }, Child = sheet };
			dim.PointerPressed += (_, _) => firstButton?.Focus();		// a click beside the buttons keeps the keyboard on the question
			dim.KeyDown += (_, e) =>
			{
				if (e.Key != Key.Escape)
					return;

				e.Handled = true;
				Answer(null);
			};

			_focusBefore = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
			_sheet = dim;
			_answer = answer;
			Children.Add(dim);
			Dispatcher.UIThread.Post(() => firstButton?.Focus(), DispatcherPriority.Loaded);
		}

		/// <summary>The way out comes first and the button that goes ahead after it. With no preference, as given.</summary>
		private static IEnumerable<int> ShownOrder(Question question, int count) =>
			question.NoPreference ? Enumerable.Range(0, count) : Enumerable.Range(0, count).Reverse();

		private void Answer(int? choice)
		{
			if (_sheet == null)
				return;

			Children.Remove(_sheet);
			_sheet = null;
			_answer?.TrySetResult(choice);
			_answer = null;
			_focusBefore?.Focus();
			_focusBefore = null;
		}


		private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(250);

		private readonly StackPanel _pills = new() { Classes = { "Pills" } };
		private Task _lastQuestion = Task.CompletedTask;
		private Border? _sheet;
		private TaskCompletionSource<int?>? _answer;
		private IInputElement? _focusBefore;
	}
}

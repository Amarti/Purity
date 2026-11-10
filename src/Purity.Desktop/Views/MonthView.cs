using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Purity.Desktop.Localization;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A month to pick a date from, in either calendar. It deals in whole dates, each given as a
	/// Gregorian day, for a Hebrew date the one holding its daytime; whether the date's night or
	/// day is meant stays with the switch beside the field.
	/// </summary>
	[TemplatePart("PART_PreviousYear", typeof(Button))]
	[TemplatePart("PART_Previous", typeof(Button))]
	[TemplatePart("PART_Title", typeof(TextBlock))]
	[TemplatePart("PART_Next", typeof(Button))]
	[TemplatePart("PART_NextYear", typeof(Button))]
	[TemplatePart("PART_Days", typeof(Grid))]
	public sealed class MonthView : TemplatedControl
	{
		/// <summary>A date was picked</summary>
		public event EventHandler<DateTime>? DayPicked;


		/// <summary>Shows the month of the selected date, or of today when there is none</summary>
		/// <param name="selected">The selected date</param>
		/// <param name="today">Today's Gregorian date. The app doesn't know sunset, so today's mark follows the civil date.</param>
		/// <param name="calendar">The calendar to show the month in</param>
		public void Show(DateTime? selected, DateTime today, CalendarKind calendar)
		{
			_selected = selected;
			_today = today.Date;
			_page = MonthPage.Of(selected ?? today, calendar);
			Build();
		}

		/// <summary>Puts the keyboard on the selected date, or on the month's first</summary>
		public void FocusDay() =>
			(_buttons.FirstOrDefault(el => el.Classes.Contains(SelectedClass)) ?? _buttons.FirstOrDefault())?.Focus();


		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_previousYear = e.NameScope.Find<Button>("PART_PreviousYear");
			_previous = e.NameScope.Find<Button>("PART_Previous");
			_title = e.NameScope.Find<TextBlock>("PART_Title");
			_next = e.NameScope.Find<Button>("PART_Next");
			_nextYear = e.NameScope.Find<Button>("PART_NextYear");
			_days = e.NameScope.Find<Grid>("PART_Days");

			Hook(_previousYear, () => _page?.PreviousYear);
			Hook(_previous, () => _page?.Previous);
			Hook(_next, () => _page?.Next);
			Hook(_nextYear, () => _page?.NextYear);
			Build();
		}


		private void Hook(Button? button, Func<MonthPage?> target)
		{
			if (button != null)
				button.Click += (_, _) => GoTo(target());
		}

		private void GoTo(MonthPage? page)
		{
			if (page == null)
				return;

			_page = page;
			Build();
		}

		private void Build()
		{
			if (_days == null || _page is not { } page)
				return;

			FlowDirection = page.Direction;
			_title!.Text = page.Title;
			foreach (var header in _days.Children.OfType<TextBlock>())
				header.Text = page.Weekdays[Grid.GetColumn(header)];
			_previousYear!.IsEnabled = page.PreviousYear != null;
			_previous!.IsEnabled = page.Previous != null;
			_next!.IsEnabled = page.Next != null;
			_nextYear!.IsEnabled = page.NextYear != null;

			foreach (var button in _buttons)
				_days.Children.Remove(button);
			_buttons.Clear();

			var offset = (int)page.Day(1).DayOfWeek;
			var theme = this.FindResource("MonthDayButtonTheme") as ControlTheme;
			for (var day = 1; day <= page.DayCount; day++)
				_buttons.Add(DayButton(page, day, day - 1 + offset, theme));
			_days.Children.AddRange(_buttons);
		}

		private Button DayButton(MonthPage page, int day, int cell, ControlTheme? theme)
		{
			var date = page.Day(day);
			var button = new Button { Content = page.Label(day), Theme = theme };
			Grid.SetColumn(button, cell % 7);
			Grid.SetRow(button, 1 + cell / 7);
			AutomationProperties.SetName(button, page.Name(day));
			button.Classes.Set(SelectedClass, date == _selected);
			button.Classes.Set(TodayClass, date == _today);
			button.Click += (_, _) => DayPicked?.Invoke(this, date);
			return button;
		}


		private const string SelectedClass = "Selected";
		private const string TodayClass = "Today";

		private readonly List<Button> _buttons = new();
		private Button? _previousYear;
		private Button? _previous;
		private TextBlock? _title;
		private Button? _next;
		private Button? _nextYear;
		private Grid? _days;
		private MonthPage? _page;
		private DateTime? _selected;
		private DateTime _today;
	}
}

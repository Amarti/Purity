using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Purity.Core;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A month of the Hebrew calendar to pick a date from, since Avalonia's calendar can only show
	/// Gregorian months. Weeks start on the right, as on a Hebrew calendar, in every language. It
	/// deals in whole Hebrew dates, each given as the Gregorian day holding its daytime; whether the
	/// date's night or day is meant stays with the switch beside the field.
	/// </summary>
	[TemplatePart("PART_PreviousYear", typeof(Button))]
	[TemplatePart("PART_Previous", typeof(Button))]
	[TemplatePart("PART_Title", typeof(TextBlock))]
	[TemplatePart("PART_Next", typeof(Button))]
	[TemplatePart("PART_NextYear", typeof(Button))]
	[TemplatePart("PART_Days", typeof(Grid))]
	public sealed class HebrewMonthView : TemplatedControl
	{
		/// <summary>A date was picked, given as the Gregorian day holding its daytime</summary>
		public event EventHandler<DateTime>? DayPicked;

		/// <summary>The month on show</summary>
		public HebrewMonth? Month { get; private set; }


		/// <summary>Shows the month of the selected date, or of today when there is none</summary>
		/// <param name="selected">The Gregorian day holding the selected date's daytime</param>
		/// <param name="today">Today's Gregorian date. The app doesn't know sunset, so today's mark follows the civil date.</param>
		public void Show(DateTime? selected, DateTime today)
		{
			_selected = selected;
			_today = today.Date;
			Month = HebrewMonth.Of(selected ?? today);
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

			Hook(_previousYear, () => Month?.InYear(Month.Year - 1));
			Hook(_previous, () => Month?.Previous);
			Hook(_next, () => Month?.Next);
			Hook(_nextYear, () => Month?.InYear(Month.Year + 1));
			Build();
		}


		private void Hook(Button? button, Func<HebrewMonth?> target)
		{
			if (button != null)
				button.Click += (_, _) => GoTo(target());
		}

		private void GoTo(HebrewMonth? month)
		{
			if (month == null)
				return;

			Month = month;
			Build();
		}

		private void Build()
		{
			if (_days == null || Month is not { } month)
				return;

			_title!.Text = month.Title;
			_previousYear!.IsEnabled = month.InYear(month.Year - 1) != null;
			_previous!.IsEnabled = month.Previous != null;
			_next!.IsEnabled = month.Next != null;
			_nextYear!.IsEnabled = month.InYear(month.Year + 1) != null;

			foreach (var button in _buttons)
				_days.Children.Remove(button);
			_buttons.Clear();

			var offset = (int)month.Daytime(1).DayOfWeek;
			var theme = this.FindResource("HebrewDayButtonTheme") as ControlTheme;
			for (var day = 1; day <= month.DayCount; day++)
				_buttons.Add(DayButton(month, day, day - 1 + offset, theme));
			_days.Children.AddRange(_buttons);
		}

		private Button DayButton(HebrewMonth month, int day, int cell, ControlTheme? theme)
		{
			var date = month.Daytime(day);
			var name = DateText.HebrewDayName(day);
			var button = new Button { Content = name, Theme = theme };
			Grid.SetColumn(button, cell % 7);
			Grid.SetRow(button, 1 + cell / 7);
			AutomationProperties.SetName(button, $"{name} {month.Name}");
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
		private DateTime? _selected;
		private DateTime _today;
	}
}

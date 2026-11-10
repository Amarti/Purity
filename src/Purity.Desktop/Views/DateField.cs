using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Purity.Desktop.Localization;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A period's date, in the calendar on show. A date is only ever picked from the month grid:
	/// typed dates would invite mistakes. It picks a whole date; night or day is the switch's to
	/// say. Nothing here listens to the mouse wheel, so scrolling past a field can't change it.
	/// </summary>
	[TemplatePart("PART_Button", typeof(Button))]
	[TemplatePart("PART_Popup", typeof(Popup))]
	[TemplatePart("PART_Month", typeof(MonthView))]
	public sealed class DateField : TemplatedControl
	{
		/// <summary>The Gregorian day picked. For a Hebrew date, the one holding its daytime.</summary>
		public static readonly StyledProperty<DateTime?> SelectedDayProperty =
			AvaloniaProperty.Register<DateField, DateTime?>(nameof(SelectedDay), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<string?> TextProperty =
			AvaloniaProperty.Register<DateField, string?>(nameof(Text));


		public DateTime? SelectedDay
		{
			get => GetValue(SelectedDayProperty);
			set => SetValue(SelectedDayProperty, value);
		}

		/// <summary>The date as shown in the field</summary>
		public string? Text
		{
			get => GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}


		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			if (_button != null)
				_button.Click -= OnButtonClick;
			if (_month != null)
			{
				_month.DayPicked -= OnDayPicked;
				_month.KeyDown -= OnMonthKeyDown;
			}
			if (_popup != null)
				_popup.Opened -= OnPopupOpened;

			_button = e.NameScope.Find<Button>("PART_Button");
			_popup = e.NameScope.Find<Popup>("PART_Popup");
			_month = e.NameScope.Find<MonthView>("PART_Month");

			_name?.Dispose();
			if (_button != null)
			{
				_button.Click += OnButtonClick;
				_name = _button.Bind(AutomationProperties.NameProperty, this.GetObservable(AutomationProperties.NameProperty));		// what a screen reader reaches is the button
			}
			if (_month != null)
			{
				_month.DayPicked += OnDayPicked;
				_month.KeyDown += OnMonthKeyDown;
			}
			if (_popup != null)
				_popup.Opened += OnPopupOpened;
		}


		private void OnButtonClick(object? sender, RoutedEventArgs e)
		{
			if (_popup == null || _month == null)
				return;

			_month.Show(SelectedDay, DateTime.Today, Loc.Instance.Calendar);
			_popup.IsOpen = !_popup.IsOpen;
		}

		private void OnPopupOpened(object? sender, EventArgs e) => _month?.FocusDay();

		private void OnDayPicked(object? sender, DateTime day)
		{
			SetCurrentValue(SelectedDayProperty, day);
			Close();
		}

		private void OnMonthKeyDown(object? sender, KeyEventArgs e)
		{
			if (e.Key != Key.Escape)
				return;

			e.Handled = true;
			Close();
		}

		private void Close()
		{
			if (_popup != null)
				_popup.IsOpen = false;
			_button?.Focus();
		}


		private IDisposable? _name;
		private Button? _button;
		private Popup? _popup;
		private MonthView? _month;
	}
}

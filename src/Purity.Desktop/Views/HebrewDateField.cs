using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// A period's date in the Hebrew calendar. It looks like the Gregorian date field, but a date is
	/// only ever picked from the month grid: typed Hebrew dates would invite mistakes. It picks a
	/// whole Hebrew date; night or day is the switch's to say.
	/// </summary>
	[TemplatePart("PART_Button", typeof(Button))]
	[TemplatePart("PART_Popup", typeof(Popup))]
	[TemplatePart("PART_Month", typeof(HebrewMonthView))]
	public sealed class HebrewDateField : TemplatedControl
	{
		/// <summary>The Gregorian day holding the daytime of the date picked</summary>
		public static readonly StyledProperty<DateTime?> SelectedDayProperty =
			AvaloniaProperty.Register<HebrewDateField, DateTime?>(nameof(SelectedDay), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<string?> TextProperty =
			AvaloniaProperty.Register<HebrewDateField, string?>(nameof(Text));


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
			_month = e.NameScope.Find<HebrewMonthView>("PART_Month");

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

			_month.Show(SelectedDay, DateTime.Today);
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
		private HebrewMonthView? _month;
	}
}

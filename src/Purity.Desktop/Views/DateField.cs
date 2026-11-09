using Avalonia.Controls;
using Avalonia.Input;


namespace Purity.Desktop.Views
{
	/// <summary>
	/// The date field of a period. Avalonia's date picker moves its date by a day for every notch of
	/// the mouse wheel while the pointer is over it, even unfocused, so scrolling the list could
	/// quietly change a period's dates. WPF's date field ignores the wheel, and so does this one:
	/// the wheel goes on to scroll the list. Use this in place of CalendarDatePicker everywhere.
	/// </summary>
	public sealed class DateField : CalendarDatePicker
	{
		/// <summary>Keeps the date picker's look and styles</summary>
		protected override Type StyleKeyOverride => typeof(CalendarDatePicker);

		/// <summary>Deliberately skips the date picker's handler, the one that changes the date</summary>
		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{}
	}
}

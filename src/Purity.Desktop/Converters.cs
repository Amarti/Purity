using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;


namespace Purity.Desktop
{
	/// <summary>The WPF app's converters</summary>
	public static class Converters
	{
		private static readonly IBrush Closed = new ImmutableSolidColorBrush(Color.FromRgb(0x56, 0x83, 0xA9));
		private static readonly IBrush Open = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x61));

		/// <summary>Frame of a period: blue once accepted, pink while still open</summary>
		public static readonly IValueConverter PeriodBorder =
			new FuncValueConverter<bool, IBrush>(closed => closed ? Closed : Open);

		/// <summary>Tooltip of the day and night buttons</summary>
		public static readonly IValueConverter DuskDawnTooltip =
			new FuncValueConverter<bool, string>(afterDusk => afterDusk ? "After dusk" : "After dawn");
	}
}

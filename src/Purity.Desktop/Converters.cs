using Avalonia.Data.Converters;


namespace Purity.Desktop
{
	/// <summary>The WPF app's converters. A period's frame color comes from the styles instead, so it has a dark version.</summary>
	public static class Converters
	{
		/// <summary>Tooltip of the day and night buttons</summary>
		public static readonly IValueConverter DuskDawnTooltip =
			new FuncValueConverter<bool, string>(afterDusk => afterDusk ? "After dusk" : "After dawn");
	}
}

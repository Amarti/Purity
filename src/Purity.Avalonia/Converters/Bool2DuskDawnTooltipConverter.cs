using System;
using System.Globalization;
using Avalonia.Data.Converters;


namespace Purity.Avalonia
{
	public class Bool2DuskDawnTooltipConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is bool boolean && boolean ? "After dusk" : "After dawn";
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

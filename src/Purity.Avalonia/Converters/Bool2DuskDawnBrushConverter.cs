using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;


namespace Purity.Avalonia
{
	public class Bool2DuskDawnBrushConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is bool boolean && boolean ? Brushes.DarkGray : Brushes.White;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

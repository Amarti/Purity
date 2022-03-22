using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;


namespace Purity.Avalonia
{
	public class BoolToBorderBrushConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return Brushes.Transparent;

			return value is bool boolean && boolean ? new SolidColorBrush(Color.FromRgb(86, 131, 169))/* aka #5683A9*/ : new SolidColorBrush(Color.FromRgb(255, 0, 97))/* aka #FF0061*/;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

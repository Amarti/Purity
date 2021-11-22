using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;


namespace Purity
{
	public class BoolToBorderBrushConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
				return Brushes.Transparent;

			return value is bool boolean && boolean ? Brushes.CornflowerBlue : Brushes.Red;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

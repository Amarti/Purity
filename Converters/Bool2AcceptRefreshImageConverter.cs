using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;


namespace Purity
{
	public class Bool2AcceptRefreshImageConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var name = value is bool boolean && boolean ? "Refresh" : "Accept";
			return Application.Current.Resources[$"{name}IconDrawingImage"];
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

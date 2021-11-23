using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;


namespace Purity
{
	public class Bool2DuskDawnBrushConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			//var name = value is bool boolean && boolean ? "Dusk" : "Dawn";
			//var im = (FrameworkElement)Application.Current.Resources[$"After{name}Icon"];
			//return im;
			return value is bool boolean && boolean ? Brushes.DarkGray : Brushes.White;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			//var name = value is bool boolean && boolean ? "Dusk" : "Dawn";
			////return (ImageSource)Application.Current.Resources[$"{name}IconDrawingImage"];
			//return (FrameworkElement)Application.Current.Resources[$"After{name}Icon"];
			throw new NotImplementedException();
		}
	}
}

﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;


namespace Purity
{
	public class Bool2NotVisibilityCollapsedConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is bool boolean && boolean ? Visibility.Collapsed : Visibility.Visible;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
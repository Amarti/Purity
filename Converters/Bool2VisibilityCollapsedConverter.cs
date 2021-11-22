﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;


namespace Purity
{
	public class Bool2VisibilityCollapsedConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is bool boolean && boolean ? Visibility.Visible : Visibility.Collapsed;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

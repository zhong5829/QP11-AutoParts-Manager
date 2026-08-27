using System;
using System.Globalization;
using System.Windows.Data;

namespace QP11.Wpf.Helpers;

/// <summary>
/// 剥离换行符，确保 DataGrid 行高不被撑大
/// </summary>
public class SingleLineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
            return s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

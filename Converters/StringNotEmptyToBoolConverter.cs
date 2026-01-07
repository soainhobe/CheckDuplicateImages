using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CheckDuplicate.Converters;

public class StringNotEmptyToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return !string.IsNullOrEmpty(s);
        }
        return true; // Non-string values (like other group keys) should be visible
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

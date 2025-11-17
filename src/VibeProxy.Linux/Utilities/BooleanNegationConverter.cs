using System;
using Avalonia.Data.Converters;

namespace VibeProxy.Linux.Utilities;

public sealed class BooleanNegationConverter : IValueConverter
{
    public static BooleanNegationConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return false;
    }
}

namespace Aero.Converters;

using System;
using System.Globalization;
using Avalonia.Data.Converters;

public class BoolToWindowStateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is bool isMaximized)
            return isMaximized
                ? Avalonia.Controls.WindowState.Maximized
                : Avalonia.Controls.WindowState.Normal;
        return Avalonia.Controls.WindowState.Normal;
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is Avalonia.Controls.WindowState state)
            return state == Avalonia.Controls.WindowState.Maximized;
        return false;
    }
}

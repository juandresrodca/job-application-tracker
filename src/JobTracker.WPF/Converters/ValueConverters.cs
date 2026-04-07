using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using JobTracker.Domain.Enums;

namespace JobTracker.WPF.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is true;
        bool invert = parameter?.ToString()?.ToLower() == "inverse";
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(SolidColorBrush))]
public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? new SolidColorBrush(Color.FromRgb(248, 81, 73)) : new SolidColorBrush(Color.FromRgb(48, 54, 61));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(ApplicationStatus), typeof(SolidColorBrush))]
public class StatusToColorConverter : IValueConverter
{
    private static readonly Dictionary<ApplicationStatus, Color> Colors = new()
    {
        [ApplicationStatus.Applied]       = Color.FromRgb(31,  111, 235),  // blue
        [ApplicationStatus.Screening]     = Color.FromRgb(130, 80,  255),  // purple
        [ApplicationStatus.Interview]     = Color.FromRgb(227, 179, 65),   // yellow
        [ApplicationStatus.TechnicalTest] = Color.FromRgb(249, 115, 22),   // orange
        [ApplicationStatus.Offer]         = Color.FromRgb(63,  185, 80),   // green
        [ApplicationStatus.Accepted]      = Color.FromRgb(22,  163, 74),   // dark green
        [ApplicationStatus.Rejected]      = Color.FromRgb(248, 81,  73),   // red
        [ApplicationStatus.Withdrawn]     = Color.FromRgb(72,  79,  88),   // grey
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ApplicationStatus status && Colors.TryGetValue(status, out var color))
            return new SolidColorBrush(color);
        return new SolidColorBrush(Color.FromRgb(72, 79, 88));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(object), typeof(bool))]
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool invert = parameter?.ToString()?.ToLower() == "inverse";
        return invert ? isNull : !isNull;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(int), typeof(string))]
public class DaysToUrgencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int days ? days switch
        {
            <= 3  => $"{days}d 🟢",
            <= 7  => $"{days}d 🟡",
            <= 14 => $"{days}d 🟠",
            _     => $"{days}d 🔴"
        } : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is true;
        // parameter format: "TrueString|FalseString"
        string[] parts = parameter?.ToString()?.Split('|') ?? new[] { "True", "False" };
        return boolValue ? parts[0] : (parts.Length > 1 ? parts[1] : "False");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

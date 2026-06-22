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
    private static readonly SolidColorBrush TrueBrush = CreateFrozenBrush(248, 81, 73);
    private static readonly SolidColorBrush FalseBrush = CreateFrozenBrush(48, 54, 61);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

[ValueConversion(typeof(ApplicationStatus), typeof(SolidColorBrush))]
public class StatusToColorConverter : IValueConverter
{
    private static readonly Dictionary<ApplicationStatus, SolidColorBrush> BrushCache = new()
    {
        [ApplicationStatus.Applied]       = CreateFrozenBrush(31,  111, 235),  // blue
        [ApplicationStatus.Screening]     = CreateFrozenBrush(130, 80,  255),  // purple
        [ApplicationStatus.Interview]     = CreateFrozenBrush(227, 179, 65),   // yellow
        [ApplicationStatus.TechnicalTest] = CreateFrozenBrush(249, 115, 22),   // orange
        [ApplicationStatus.Offer]         = CreateFrozenBrush(63,  185, 80),   // green
        [ApplicationStatus.Accepted]      = CreateFrozenBrush(22,  163, 74),   // dark green
        [ApplicationStatus.Rejected]      = CreateFrozenBrush(248, 81,  73),   // red
        [ApplicationStatus.Withdrawn]     = CreateFrozenBrush(72,  79,  88),   // grey
    };

    private static readonly SolidColorBrush DefaultBrush = CreateFrozenBrush(72, 79, 88);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ApplicationStatus status && BrushCache.TryGetValue(status, out var brush)
            ? brush
            : DefaultBrush;

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

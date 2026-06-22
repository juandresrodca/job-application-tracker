using FluentAssertions;
using Xunit;
using System.Windows;
using JobTracker.WPF.Converters;
using System.Globalization;

namespace JobTracker.Tests.Converters;

/// <summary>
/// Tests for WPF Value Converters.
/// Note: These require a project reference to JobTracker.WPF and a net8.0-windows target.
/// </summary>
public class ValueConverterTests
{
    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void BoolToVisibilityConverter_ConvertsCorrectly(bool input, Visibility expected)
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void BoolToVisibilityConverter_WithInverseParameter_ConvertsCorrectly(bool input, Visibility expected)
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(input, typeof(Visibility), "inverse", CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void NullToVisibilityConverter_ReturnsCollapsed_ForNull()
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert(null, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void NullToVisibilityConverter_ReturnsVisible_ForObject()
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert(new object(), typeof(Visibility), string.Empty, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Visible);
    }

    // Note: StatusToColorConverter and DaysToUrgencyConverter might require 
    // mocking or setting up Application.Current.Resources if they perform resource lookups.
}
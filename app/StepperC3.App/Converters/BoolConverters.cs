using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StepperC3.App.Converters;

/// <summary>Converts a boolean to Visibility (true=Visible, false=Collapsed).</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>Inverts a boolean value.</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is false;
}

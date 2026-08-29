using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ReportExpert.App.Helpers;

/// <summary>Shows an element when an integer count is greater than zero.</summary>
internal sealed class CollectionCountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

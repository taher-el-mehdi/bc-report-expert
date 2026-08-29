using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ReportExpert.Modules.Copilot.ViewModels;

namespace ReportExpert.Modules.Copilot.Helpers;

/// <summary>
/// Chooses between the plain message bubble and the tool call card.
/// </summary>
public sealed class CopilotMessageTemplateSelector : DataTemplateSelector
{
    /// <summary>Template for a message from the user or the assistant.</summary>
    public DataTemplate? ChatTemplate { get; set; }

    /// <summary>Template for a tool call card.</summary>
    public DataTemplate? ToolCallTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
    {
        ToolCallMessageViewModel => ToolCallTemplate,
        _ => ChatTemplate,
    };
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

public sealed class UserToAlignmentConverter : IValueConverter
{
    /// <summary>
    /// User chips sit on the right; Rex messages stretch so markdown can use the full pane width.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

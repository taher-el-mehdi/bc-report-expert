using System.Windows;

namespace ReportExpert.RdlcDesigner.Surface;

/// <summary>
/// Attached metadata linking a visual to a document item id.
/// </summary>
public static class DesignElement
{
    public static readonly DependencyProperty ItemIdProperty = DependencyProperty.RegisterAttached(
        "ItemId",
        typeof(string),
        typeof(DesignElement),
        new FrameworkPropertyMetadata(null));

    public static void SetItemId(DependencyObject element, string? value) =>
        element.SetValue(ItemIdProperty, value);

    public static string? GetItemId(DependencyObject element) =>
        (string?)element.GetValue(ItemIdProperty);
}

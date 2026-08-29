using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class RectanglePropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind == ReportItemKind.Rectangle;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        IPropertyService props = context.Properties;

        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, props);
        PropertyProviderHelpers.AddHidden(general, item, props);

        var appearance = new List<PropertyDefinition>
        {
            new("BackgroundColor", "Fill color", PropertyEditorKind.Color,
                item.BackgroundColor ?? string.Empty,
                v => props.SetAppearance(
                    item,
                    NullEmpty(v),
                    item.BorderColor,
                    item.BorderStyle,
                    item.BorderWidth)),
            new("BorderColor", "Border color", PropertyEditorKind.Color,
                item.BorderColor ?? string.Empty,
                v => props.SetAppearance(
                    item,
                    item.BackgroundColor,
                    NullEmpty(v),
                    item.BorderStyle ?? "Solid",
                    item.BorderWidth)),
            new("BorderStyle", "Border style", PropertyEditorKind.BorderStyle,
                item.BorderStyle ?? "Solid",
                v =>
                {
                    string? style = NullEmpty(v);
                    if (string.Equals(style, "None", StringComparison.OrdinalIgnoreCase))
                        style = "None";
                    props.SetAppearance(item, item.BackgroundColor, item.BorderColor, style, item.BorderWidth);
                }),
            new("BorderWidth", "Border width", PropertyEditorKind.Text,
                item.BorderWidth ?? string.Empty,
                v => props.SetAppearance(
                    item,
                    item.BackgroundColor,
                    item.BorderColor,
                    item.BorderStyle ?? "Solid",
                    NullEmpty(v)),
                description: "e.g. 1pt")
        };

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, props);

        return
        [
            PropertyProviderHelpers.Category("Rectangle", general),
            PropertyProviderHelpers.Category("Appearance", appearance),
            PropertyProviderHelpers.Category("Layout", layout)
        ];
    }

    private static string? NullEmpty(object? v)
    {
        string? s = v?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}

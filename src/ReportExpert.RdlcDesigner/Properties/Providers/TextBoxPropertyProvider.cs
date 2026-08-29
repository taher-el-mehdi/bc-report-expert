using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class TextBoxPropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind is ReportItemKind.Textbox or ReportItemKind.TablixCellTextbox;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        IPropertyService props = context.Properties;

        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, props);
        if (item.CanEditValue)
        {
            general.Add(new PropertyDefinition(
                "Value",
                "Value",
                PropertyEditorKind.Expression,
                item.Value ?? string.Empty,
                v => props.SetValue(item, v?.ToString())));
        }

        PropertyProviderHelpers.AddHidden(general, item, props);

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, props);

        var font = new List<PropertyDefinition>();
        if (item.CanEditStyle)
        {
            font.Add(new PropertyDefinition("FontFamily", "Font family", PropertyEditorKind.FontFamily,
                item.FontFamily ?? string.Empty,
                v => props.SetFont(item, NullEmpty(v), item.FontSize, item.FontWeight, item.Color)));
            font.Add(new PropertyDefinition("FontSize", "Font size", PropertyEditorKind.FontSize,
                item.FontSize ?? string.Empty,
                v => props.SetFont(item, item.FontFamily, NullEmpty(v), item.FontWeight, item.Color)));
            font.Add(new PropertyDefinition("FontWeight", "Font weight", PropertyEditorKind.FontWeight,
                item.FontWeight ?? "Default",
                v =>
                {
                    string? w = v?.ToString();
                    if (w is "Default" or null or "")
                        w = null;
                    props.SetFont(item, item.FontFamily, item.FontSize, w, item.Color);
                }));
            font.Add(new PropertyDefinition("Color", "Color", PropertyEditorKind.Color,
                item.Color ?? string.Empty,
                v => props.SetFont(item, item.FontFamily, item.FontSize, item.FontWeight, NullEmpty(v))));
        }

        var categories = new List<PropertyCategory>
        {
            PropertyProviderHelpers.Category("TextBox", general)
        };
        if (layout.Count > 0)
            categories.Add(PropertyProviderHelpers.Category("Layout", layout));
        if (font.Count > 0)
            categories.Add(PropertyProviderHelpers.Category("Font", font));
        return categories;
    }

    private static string? NullEmpty(object? v)
    {
        string? s = v?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}

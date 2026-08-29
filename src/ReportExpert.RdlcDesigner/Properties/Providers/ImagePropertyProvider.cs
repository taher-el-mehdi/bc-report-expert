using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class ImagePropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind == ReportItemKind.Image;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        IPropertyService props = context.Properties;

        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, props);
        PropertyProviderHelpers.AddHidden(general, item, props);

        // External-only for now: MIME type is not used; Value is a local image file path.
        string path = item.ImageValue ?? string.Empty;
        var image = new List<PropertyDefinition>
        {
            new("ImageSource", "Source", PropertyEditorKind.ReadOnlyLabel,
                "External",
                _ => { },
                isReadOnly: true,
                description: "Only External images are supported currently."),
            new("ImageValue", "Image file", PropertyEditorKind.ImageFilePath,
                path,
                v => props.SetImage(item, "External", NormalizeExternalPath(v?.ToString()), mimeType: null))
        };

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, props);

        return
        [
            PropertyProviderHelpers.Category("Image", general),
            PropertyProviderHelpers.Category("Source", image),
            PropertyProviderHelpers.Category("Layout", layout)
        ];
    }

    /// <summary>
    /// Stores a ReportViewer-friendly file URI when given a local path.
    /// </summary>
    public static string? NormalizeExternalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim().Trim('"');
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return value;

        if (!System.IO.Path.IsPathRooted(value))
            return value;

        try
        {
            string full = System.IO.Path.GetFullPath(value);
            return new Uri(full).AbsoluteUri; // file:///C:/...
        }
        catch
        {
            return value;
        }
    }
}

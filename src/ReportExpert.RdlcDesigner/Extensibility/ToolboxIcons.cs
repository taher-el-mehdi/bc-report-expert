using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// Loads shared Report Designer toolbox icons from Resources/Icons.
/// </summary>
public static class ToolboxIcons
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Get(string fileName)
    {
        if (Cache.TryGetValue(fileName, out ImageSource? cached))
            return cached;

        try
        {
            var uri = new Uri(
                $"pack://application:,,,/ReportExpert.RdlcDesigner;component/Resources/Icons/{fileName}",
                UriKind.Absolute);
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            Cache[fileName] = image;
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? ForKind(ToolboxItemKind kind) => kind switch
    {
        ToolboxItemKind.Pointer => Get("Pointer.png"),
        ToolboxItemKind.TextBox => Get("TextBox.png"),
        ToolboxItemKind.Rectangle => Get("Rectangle.png"),
        ToolboxItemKind.Line => Get("Line.png"),
        ToolboxItemKind.Image => Get("Image.png"),
        ToolboxItemKind.Table => Get("Table.png"),
        ToolboxItemKind.Matrix => Get("Matrix.png"),
        ToolboxItemKind.List => Get("List.png"),
        ToolboxItemKind.Chart => Get("Chart.png"),
        ToolboxItemKind.Subreport => Get("Subreport.png"),
        ToolboxItemKind.Gauge => Get("Gauge.png"),
        _ => null
    };
}

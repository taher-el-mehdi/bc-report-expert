using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ReportExpert.RdlcDesigner.Surface;

/// <summary>
/// Paint-style tile grid (1/8 inch minor, 1 inch major) for the design page.
/// </summary>
internal static class DesignGridBrush
{
    private const double InchPx = 96.0;
    private const double Minor = InchPx / 8.0;

    public static Brush CreateWithMajors(bool dark)
    {
        Color minorColor = dark
            ? Color.FromArgb(0x50, 0xA0, 0xA0, 0xA0)
            : Color.FromArgb(0x60, 0xB8, 0xB8, 0xB8);
        Color majorColor = dark
            ? Color.FromArgb(0x90, 0xC8, 0xC8, 0xC8)
            : Color.FromArgb(0x90, 0x88, 0x88, 0x88);

        var group = new DrawingGroup();

        var minorPen = new Pen(new SolidColorBrush(minorColor), 0.7);
        minorPen.Freeze();
        for (double x = Minor; x < InchPx; x += Minor)
            group.Children.Add(new GeometryDrawing(null, minorPen, new LineGeometry(new Point(x, 0), new Point(x, InchPx))));
        for (double y = Minor; y < InchPx; y += Minor)
            group.Children.Add(new GeometryDrawing(null, minorPen, new LineGeometry(new Point(0, y), new Point(InchPx, y))));

        var majorPen = new Pen(new SolidColorBrush(majorColor), 1);
        majorPen.Freeze();
        group.Children.Add(new GeometryDrawing(null, majorPen, new LineGeometry(new Point(0, 0), new Point(0, InchPx))));
        group.Children.Add(new GeometryDrawing(null, majorPen, new LineGeometry(new Point(0, 0), new Point(InchPx, 0))));
        group.Freeze();

        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, InchPx, InchPx),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    public static Rectangle CreateOverlay(bool dark) =>
        new()
        {
            Fill = CreateWithMajors(dark),
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
}

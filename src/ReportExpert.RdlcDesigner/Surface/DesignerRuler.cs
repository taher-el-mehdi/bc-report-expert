using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReportExpert.RdlcDesigner.Theming;

namespace ReportExpert.RdlcDesigner.Surface;

/// <summary>
/// Top or left measurement ruler that tracks design-surface scroll and zoom (inches at 96 DPI).
/// </summary>
public sealed class DesignerRuler : FrameworkElement
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(DesignerRuler),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(DesignerRuler),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ContentOriginProperty =
        DependencyProperty.Register(
            nameof(ContentOrigin),
            typeof(double),
            typeof(DesignerRuler),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double InchPx = 96.0;
    private const double MinorDivisions = 8; // 1/8 inch like Paint

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>Where content coordinate 0 maps onto this ruler (device-independent pixels).</summary>
    public double ContentOrigin
    {
        get => (double)GetValue(ContentOriginProperty);
        set => SetValue(ContentOriginProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double zoom = Zoom <= 0 ? 1 : Zoom;
        double step = InchPx * zoom / MinorDivisions;
        if (step < 2)
            step = 2;

        bool horizontal = Orientation == Orientation.Horizontal;
        double length = horizontal ? ActualWidth : ActualHeight;
        double thickness = horizontal ? ActualHeight : ActualWidth;
        if (length <= 0 || thickness <= 0)
            return;

        Brush background = DesignerTheme.IsDark
            ? DesignerTheme.Brush(0x32, 0x32, 0x32)
            : DesignerTheme.Brush(0xF0, 0xF0, 0xF0);
        Brush tickBrush = DesignerTheme.IsDark
            ? DesignerTheme.Brush(0xA8, 0xA8, 0xA8)
            : DesignerTheme.Brush(0x66, 0x66, 0x66);
        Brush majorBrush = DesignerTheme.IsDark
            ? DesignerTheme.Brush(0xD0, 0xD0, 0xD0)
            : DesignerTheme.Brush(0x33, 0x33, 0x33);
        Brush textBrush = DesignerTheme.SecondaryForeground;

        dc.DrawRectangle(background, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var pen = new Pen(tickBrush, 1);
        var majorPen = new Pen(majorBrush, 1);
        pen.Freeze();
        majorPen.Freeze();

        double origin = ContentOrigin;
        int first = (int)Math.Floor((-origin) / step) - 1;
        int last = (int)Math.Ceiling((length - origin) / step) + 1;

        var typeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        for (int i = first; i <= last; i++)
        {
            double pos = origin + i * step;
            if (pos < -1 || pos > length + 1)
                continue;

            bool major = i % (int)MinorDivisions == 0;
            double tickLen = major ? thickness * 0.85 : (i % 2 == 0 ? thickness * 0.45 : thickness * 0.28);
            Pen usePen = major ? majorPen : pen;

            if (horizontal)
            {
                dc.DrawLine(usePen, new Point(pos, thickness), new Point(pos, thickness - tickLen));
                if (major)
                {
                    double inches = i / MinorDivisions;
                    var text = new FormattedText(
                        inches.ToString("0.##", CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        9,
                        textBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(text, new Point(pos + 2, 1));
                }
            }
            else
            {
                dc.DrawLine(usePen, new Point(thickness, pos), new Point(thickness - tickLen, pos));
                if (major)
                {
                    double inches = i / MinorDivisions;
                    var text = new FormattedText(
                        inches.ToString("0.##", CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        9,
                        textBrush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    // Rotate label along the vertical ruler
                    dc.PushTransform(new RotateTransform(-90, thickness - 12, pos + 2));
                    dc.DrawText(text, new Point(thickness - 12, pos + 2));
                    dc.Pop();
                }
            }
        }

        // Edge line toward the canvas
        if (horizontal)
            dc.DrawLine(majorPen, new Point(0, thickness - 0.5), new Point(length, thickness - 0.5));
        else
            dc.DrawLine(majorPen, new Point(thickness - 0.5, 0), new Point(thickness - 0.5, length));
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ReportExpert.RdlcDesigner.Surface;

/// <summary>
/// Selection outline with eight resize handles.
/// </summary>
public sealed class SelectionAdorner : Adorner
{
    public enum Handle
    {
        None,
        NW, N, NE,
        W, E,
        SW, S, SE,
        Move
    }

    private static readonly SolidColorBrush OutlineBrush = CreateBrush(0x00, 0x78, 0xD4);
    private static readonly SolidColorBrush HandleFill = CreateBrush(0xFF, 0xFF, 0xFF);
    private const double HandleSize = 8;

    private readonly VisualCollection _visuals;
    private readonly Rectangle _outline;
    private readonly Rectangle[] _handles;
    private readonly Rect[] _handleRects = new Rect[8];

    public SelectionAdorner(UIElement adorned) : base(adorned)
    {
        _visuals = new VisualCollection(this);
        _outline = new Rectangle
        {
            Stroke = OutlineBrush,
            StrokeThickness = 1.5,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        _visuals.Add(_outline);

        _handles = new Rectangle[8];
        for (int i = 0; i < 8; i++)
        {
            _handles[i] = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = HandleFill,
                Stroke = OutlineBrush,
                StrokeThickness = 1
            };
            _visuals.Add(_handles[i]);
        }

        IsHitTestVisible = true;
    }

    public Handle HitTestHandle(Point relativeToAdorner)
    {
        for (int i = 0; i < _handleRects.Length; i++)
        {
            if (_handleRects[i].Contains(relativeToAdorner))
                return (Handle)(i + 1);
        }

        var bounds = new Rect(AdornedElement.RenderSize);
        return bounds.Contains(relativeToAdorner) ? Handle.Move : Handle.None;
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;
        _outline.Arrange(new Rect(-1, -1, w + 2, h + 2));

        Place(0, -HandleSize / 2, -HandleSize / 2, Cursors.SizeNWSE);
        Place(1, w / 2 - HandleSize / 2, -HandleSize / 2, Cursors.SizeNS);
        Place(2, w - HandleSize / 2, -HandleSize / 2, Cursors.SizeNESW);
        Place(3, -HandleSize / 2, h / 2 - HandleSize / 2, Cursors.SizeWE);
        Place(4, w - HandleSize / 2, h / 2 - HandleSize / 2, Cursors.SizeWE);
        Place(5, -HandleSize / 2, h - HandleSize / 2, Cursors.SizeNESW);
        Place(6, w / 2 - HandleSize / 2, h - HandleSize / 2, Cursors.SizeNS);
        Place(7, w - HandleSize / 2, h - HandleSize / 2, Cursors.SizeNWSE);

        return finalSize;
    }

    private void Place(int index, double x, double y, Cursor cursor)
    {
        _handleRects[index] = new Rect(x, y, HandleSize, HandleSize);
        _handles[index].Cursor = cursor;
        _handles[index].Arrange(_handleRects[index]);
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

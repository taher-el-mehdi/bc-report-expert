using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.ContextMenus;
using ReportExpert.RdlcDesigner.Extensibility;
using ReportExpert.RdlcDesigner.Model;
using ReportExpert.RdlcDesigner.Services;
using ReportExpert.RdlcDesigner.Theming;

namespace ReportExpert.RdlcDesigner.Surface;

/// <summary>
/// Interactive design canvas: render, select, move, resize, drop toolbox items.
/// </summary>
public sealed class DesignSurface : IDesignSurface
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;
    public const double ZoomStep = 0.1;

    private readonly Grid _root = new() { Background = Brushes.Transparent, AllowDrop = true, Focusable = true };
    private readonly AdornerDecorator _adornerHost = new();
    private readonly Grid _pageContainer = new();
    private readonly ContentControl _pageHost = new();
    private readonly Rectangle _gridOverlay;
    private readonly ScaleTransform _zoomTransform = new(1, 1);
    private readonly RdlcVisualRenderer _renderer = new();
    private readonly ISelectionService _selection;
    private readonly IPropertyService _properties;

    private RdlcDocument? _document;
    private FrameworkElement? _page;
    private SelectionAdorner? _adorner;
    private bool _dragging;
    private SelectionAdorner.Handle _activeHandle;
    private Point _dragStart;
    private double _origL, _origT, _origW, _origH;
    private FrameworkElement? _dragVisual;
    private ReportItemModel? _dragItem;
    private DesignerContextMenuBuilder? _contextMenus;
    private IReportItemFactory? _factory;
    private IToolboxService? _toolbox;
    private double _zoom = 1.0;
    private bool _showGridlines = true;

    public DesignSurface(ISelectionService selection, IPropertyService properties)
    {
        _selection = selection;
        _properties = properties;
        _gridOverlay = DesignGridBrush.CreateOverlay(DesignerTheme.IsDark);
        _gridOverlay.Visibility = Visibility.Visible;

        _pageContainer.Children.Add(_pageHost);
        _pageContainer.Children.Add(_gridOverlay);
        _adornerHost.Child = _pageContainer;
        _root.Children.Add(_adornerHost);
        _root.LayoutTransform = _zoomTransform;

        _root.PreviewMouseLeftButtonDown += OnMouseDown;
        _root.PreviewMouseMove += OnMouseMove;
        _root.PreviewMouseLeftButtonUp += OnMouseUp;
        _root.MouseLeave += (_, _) => EndDrag(commit: false);
        _root.PreviewMouseRightButtonDown += OnRightMouseDown;
        _root.DragOver += OnDragOver;
        _root.Drop += OnDrop;
        _selection.SelectionChanged += (_, _) => UpdateAdorner();
    }

    public FrameworkElement Root => _root;
    public double PageWidthPx { get; private set; }

    public event EventHandler? ZoomChanged;

    public double Zoom
    {
        get => _zoom;
        set
        {
            double clamped = Math.Clamp(value, MinZoom, MaxZoom);
            // Snap lightly so 100% is easy to hit
            if (Math.Abs(clamped - 1.0) < 0.01)
                clamped = 1.0;
            if (Math.Abs(clamped - _zoom) < 0.0001)
                return;

            _zoom = clamped;
            _zoomTransform.ScaleX = _zoom;
            _zoomTransform.ScaleY = _zoom;
            ZoomChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ShowGridlines
    {
        get => _showGridlines;
        set
        {
            _showGridlines = value;
            _gridOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public void ZoomIn() => Zoom = Math.Min(MaxZoom, _zoom + ZoomStep);

    public void ZoomOut() => Zoom = Math.Max(MinZoom, _zoom - ZoomStep);

    public void ResetZoom() => Zoom = 1.0;

    public void RefreshGridTheme()
    {
        _gridOverlay.Fill = DesignGridBrush.CreateWithMajors(DesignerTheme.IsDark);
    }

    public void AttachInteractions(
        IReportItemFactory factory,
        IToolboxService toolbox,
        DesignerContextMenuBuilder contextMenus)
    {
        _factory = factory;
        _toolbox = toolbox;
        _contextMenus = contextMenus;
    }

    public void Bind(IRdlcDocument document)
    {
        _document = document as RdlcDocument
            ?? throw new ArgumentException("Expected RdlcDocument.", nameof(document));
        Refresh();
    }

    public void Refresh()
    {
        ClearAdorner();
        if (_document is null)
        {
            _pageHost.Content = null;
            _page = null;
            PageWidthPx = 0;
            return;
        }

        (FrameworkElement element, _) = _renderer.Build(_document);
        _page = element;
        PageWidthPx = _document.PageWidthPx;
        _pageHost.Content = element;
        UpdateAdorner();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (ToolboxDragFormats.TryGetKind(e.Data, out _))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (_factory is null || !ToolboxDragFormats.TryGetKind(e.Data, out ToolboxItemKind kind))
            return;

        Point pagePos = e.GetPosition(_page ?? _root);
        pagePos = ClampToPage(pagePos);
        IReportItem? created = _factory.Create(kind, pagePos.X, pagePos.Y);
        if (created is not null)
            _toolbox?.SetActiveTool(ToolboxItemKind.Pointer);
        e.Handled = true;
    }

    private void OnRightMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_document is null || _contextMenus is null)
            return;

        string? id = HitTestItemId(e.OriginalSource as DependencyObject);
        if (id is not null)
        {
            ReportItemModel? item = _document.FindModel(id);
            if (item is not null && !_selection.Selected.Any(s => ReferenceEquals(s, item)))
                _selection.Select(item);
        }
        else if (_selection.Selected.Count > 0 && Keyboard.Modifiers != ModifierKeys.Control)
        {
            // Keep selection when right-clicking empty space only if Ctrl not held;
            // VS clears when clicking empty — match that.
            _selection.Clear();
        }

        Point insertPoint = ClampToPage(e.GetPosition(_page ?? _root));
        ContextMenu menu = _contextMenus.Build(insertPoint);
        menu.PlacementTarget = _root;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_document is null)
            return;

        Point pos = e.GetPosition(_root);
        if (_adorner is not null && _selection.Primary is ReportItemModel selected &&
            selected.CanEditGeometry &&
            _renderer.Visuals.TryGetValue(selected.Id, out FrameworkElement? selectedVisual))
        {
            Point adornerPos = e.GetPosition(_adorner);
            var handle = _adorner.HitTestHandle(adornerPos);
            if (handle != SelectionAdorner.Handle.None &&
                IsOverSelectedVisual(pos, selectedVisual))
            {
                BeginDrag(selected, selectedVisual, handle, e.GetPosition(_page ?? _root));
                e.Handled = true;
                return;
            }
        }

        string? id = HitTestItemId(e.OriginalSource as DependencyObject);
        if (id is null)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                _selection.Clear();
            return;
        }

        ReportItemModel? item = _document.FindModel(id);
        if (item is null)
            return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
            _selection.Toggle(item);
        else
            _selection.Select(item);

        if (Keyboard.Modifiers != ModifierKeys.Control &&
            item.CanEditGeometry &&
            _renderer.Visuals.TryGetValue(item.Id, out FrameworkElement? visual))
        {
            BeginDrag(item, visual, SelectionAdorner.Handle.Move, e.GetPosition(_page ?? _root));
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _dragItem is null || _dragVisual is null || _page is null)
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag(commit: true);
            return;
        }

        Point pos = e.GetPosition(_page);
        double dx = pos.X - _dragStart.X;
        double dy = pos.Y - _dragStart.Y;
        ApplyPreview(_activeHandle, dx, dy);
        UpdateAdorner();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e) => EndDrag(commit: true);

    private void BeginDrag(
        ReportItemModel item,
        FrameworkElement visual,
        SelectionAdorner.Handle handle,
        Point start)
    {
        _dragging = true;
        _dragItem = item;
        _dragVisual = visual;
        _activeHandle = handle;
        _dragStart = start;
        _origL = item.LeftPx;
        _origT = item.TopPx;
        _origW = item.WidthPx;
        _origH = item.HeightPx;
        _root.CaptureMouse();
    }

    private void EndDrag(bool commit)
    {
        if (!_dragging || _dragItem is null)
        {
            _dragging = false;
            _root.ReleaseMouseCapture();
            return;
        }

        double left = Canvas.GetLeft(_dragVisual!);
        double top = Canvas.GetTop(_dragVisual!);
        if (double.IsNaN(left)) left = _origL;
        if (double.IsNaN(top)) top = _origT;
        double width = _dragVisual!.Width > 0 ? _dragVisual.Width : _origW;
        double height = _dragVisual.Height > 0 ? _dragVisual.Height : _origH;

        _dragging = false;
        _root.ReleaseMouseCapture();

        if (commit)
        {
            _properties.SetGeometry(_dragItem, left, top, width, height);
        }
        else
        {
            Canvas.SetLeft(_dragVisual, _origL);
            Canvas.SetTop(_dragVisual, _origT);
            _dragVisual.Width = _origW;
            _dragVisual.Height = _origH;
        }

        _dragItem = null;
        _dragVisual = null;
        UpdateAdorner();
    }

    private void ApplyPreview(SelectionAdorner.Handle handle, double dx, double dy)
    {
        if (_dragVisual is null)
            return;

        double l = _origL, t = _origT, w = _origW, h = _origH;
        switch (handle)
        {
            case SelectionAdorner.Handle.Move:
                l = Math.Max(0, _origL + dx);
                t = Math.Max(0, _origT + dy);
                break;
            case SelectionAdorner.Handle.E:
                w = Math.Max(8, _origW + dx);
                break;
            case SelectionAdorner.Handle.W:
                l = Math.Max(0, _origL + dx);
                w = Math.Max(8, _origW - dx);
                break;
            case SelectionAdorner.Handle.S:
                h = Math.Max(8, _origH + dy);
                break;
            case SelectionAdorner.Handle.N:
                t = Math.Max(0, _origT + dy);
                h = Math.Max(8, _origH - dy);
                break;
            case SelectionAdorner.Handle.SE:
                w = Math.Max(8, _origW + dx);
                h = Math.Max(8, _origH + dy);
                break;
            case SelectionAdorner.Handle.NW:
                l = Math.Max(0, _origL + dx);
                t = Math.Max(0, _origT + dy);
                w = Math.Max(8, _origW - dx);
                h = Math.Max(8, _origH - dy);
                break;
            case SelectionAdorner.Handle.NE:
                t = Math.Max(0, _origT + dy);
                w = Math.Max(8, _origW + dx);
                h = Math.Max(8, _origH - dy);
                break;
            case SelectionAdorner.Handle.SW:
                l = Math.Max(0, _origL + dx);
                w = Math.Max(8, _origW - dx);
                h = Math.Max(8, _origH + dy);
                break;
        }

        Canvas.SetLeft(_dragVisual, l);
        Canvas.SetTop(_dragVisual, t);
        _dragVisual.Width = w;
        _dragVisual.Height = h;
    }

    private void UpdateAdorner()
    {
        ClearAdorner();
        if (_selection.Primary is not ReportItemModel item)
            return;
        if (!_renderer.Visuals.TryGetValue(item.Id, out FrameworkElement? visual))
            return;

        var layer = AdornerLayer.GetAdornerLayer(visual);
        if (layer is null)
            return;

        _adorner = new SelectionAdorner(visual);
        layer.Add(_adorner);
    }

    private void ClearAdorner()
    {
        if (_adorner is null)
            return;
        var layer = AdornerLayer.GetAdornerLayer(_adorner.AdornedElement);
        layer?.Remove(_adorner);
        _adorner = null;
    }

    private Point ClampToPage(Point p)
    {
        double maxX = _page?.ActualWidth > 0 ? _page.ActualWidth : PageWidthPx;
        double maxY = _page?.ActualHeight > 0 ? _page.ActualHeight : 800;
        return new Point(
            Math.Clamp(p.X, 0, Math.Max(0, maxX - 8)),
            Math.Clamp(p.Y, 0, Math.Max(0, maxY - 8)));
    }

    private static bool IsOverSelectedVisual(Point rootPos, FrameworkElement visual)
    {
        try
        {
            GeneralTransform t = visual.TransformToAncestor(visual.Parent as Visual ?? visual);
            _ = rootPos;
            _ = t;
            return true;
        }
        catch
        {
            return true;
        }
    }

    private static string? HitTestItemId(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            string? id = DesignElement.GetItemId(current);
            if (!string.IsNullOrEmpty(id))
                return id;
            current = LogicalTreeHelper.GetParent(current)
                ?? (current is Visual v ? VisualTreeHelper.GetParent(v) : null);
        }

        return null;
    }
}

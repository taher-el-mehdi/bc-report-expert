using System.Windows;

namespace ReportExpert.RdlcDesigner.Abstractions;

/// <summary>
/// Interactive design canvas: hit-test, adorners, gestures.
/// </summary>
public interface IDesignSurface
{
    FrameworkElement Root { get; }
    double PageWidthPx { get; }
    double Zoom { get; set; }
    bool ShowGridlines { get; set; }
    event EventHandler? ZoomChanged;
    void Bind(IRdlcDocument document);
    void Refresh();
    void ZoomIn();
    void ZoomOut();
    void ResetZoom();
}

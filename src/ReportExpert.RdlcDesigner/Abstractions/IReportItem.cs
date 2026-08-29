namespace ReportExpert.RdlcDesigner.Abstractions;

public enum ReportItemKind
{
    Textbox,
    Rectangle,
    Tablix,
    TablixCellTextbox,
    Line,
    Image,
    Chart,
    Subreport,
    Gauge,
    Other
}

/// <summary>
/// Stable editable report item (geometry and/or value).
/// </summary>
public interface IReportItem
{
    string Id { get; }
    ReportItemKind Kind { get; }
    string Name { get; }
    double LeftPx { get; }
    double TopPx { get; }
    double WidthPx { get; }
    double HeightPx { get; }
    string? Value { get; }
    string? FontFamily { get; }
    string? FontSize { get; }
    string? FontWeight { get; }
    string? Color { get; }
    bool Hidden { get; }
    string? ImageSource { get; }
    string? ImageValue { get; }
    string? ImageMimeType { get; }
    string? BackgroundColor { get; }
    string? BorderColor { get; }
    string? BorderStyle { get; }
    string? BorderWidth { get; }
    bool CanEditGeometry { get; }
    bool CanEditValue { get; }
    bool CanEditName { get; }
    bool CanEditStyle { get; }
    bool CanEditImage { get; }
    bool CanEditAppearance { get; }
}

namespace ReportExpert.RdlcDesigner.Abstractions;

public interface IPropertyService
{
    void SetName(IReportItem item, string name);
    void SetValue(IReportItem item, string? value);
    void SetGeometry(IReportItem item, double leftPx, double topPx, double widthPx, double heightPx);
    void SetFont(IReportItem item, string? family, string? size, string? weight, string? color);
    void SetHidden(IReportItem item, bool hidden);
    void SetImage(IReportItem item, string source, string? value, string? mimeType);
    void SetAppearance(IReportItem item, string? backgroundColor, string? borderColor, string? borderStyle, string? borderWidth);
}

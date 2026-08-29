namespace ReportExpert.RdlcDesigner.Abstractions;

public interface ISelectionService
{
    IReportItem? Primary { get; }
    IReadOnlyList<IReportItem> Selected { get; }
    event EventHandler? SelectionChanged;
    void Select(IReportItem? item);
    void SelectMany(IEnumerable<IReportItem> items, IReportItem? primary = null);
    void Toggle(IReportItem item);
    void Clear();
}

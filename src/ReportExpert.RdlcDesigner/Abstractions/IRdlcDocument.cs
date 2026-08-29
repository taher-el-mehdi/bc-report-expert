namespace ReportExpert.RdlcDesigner.Abstractions;

/// <summary>
/// In-memory RDLC document with editable report items.
/// </summary>
public interface IRdlcDocument
{
    string? FilePath { get; }
    double PageWidthPx { get; }
    IReadOnlyList<IReportItem> Items { get; }
    IReportItem? Find(string id);
    event EventHandler? Changed;
}

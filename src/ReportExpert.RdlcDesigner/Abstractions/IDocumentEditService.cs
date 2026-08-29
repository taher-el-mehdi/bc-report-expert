using ReportExpert.RdlcDesigner.Extensibility;

namespace ReportExpert.RdlcDesigner.Abstractions;

/// <summary>
/// Insert / delete report items and apply field bindings.
/// </summary>
public interface IDocumentEditService
{
    IReportItem? Insert(ToolboxItemKind kind, double leftPx, double topPx);
    bool Delete(IReportItem item);
    IReportItem? InsertFieldTextbox(string datasetName, string fieldName, double leftPx, double topPx);
    void BindFieldToSelected(string fieldName);
}

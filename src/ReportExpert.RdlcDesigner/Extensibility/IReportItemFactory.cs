using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Extensibility;

namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// Creates report items from toolbox kinds via <see cref="IDocumentEditService"/>.
/// </summary>
public interface IReportItemFactory
{
    IReadOnlyList<ToolboxItemKind> SupportedKinds { get; }
    IReportItem? Create(ToolboxItemKind kind, double leftPx, double topPx);
}

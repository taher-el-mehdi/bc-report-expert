using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// Default factory that delegates insert to <see cref="IDocumentEditService"/>.
/// </summary>
public sealed class ReportItemFactory : IReportItemFactory
{
    private readonly IDocumentEditService _edits;

    public ReportItemFactory(IDocumentEditService edits)
    {
        _edits = edits;
    }

    public IReadOnlyList<ToolboxItemKind> SupportedKinds { get; } =
    [
        ToolboxItemKind.TextBox,
        ToolboxItemKind.Rectangle,
        ToolboxItemKind.Line,
        ToolboxItemKind.Image,
        ToolboxItemKind.Table,
        ToolboxItemKind.Matrix,
        ToolboxItemKind.List,
        ToolboxItemKind.Chart,
        ToolboxItemKind.Subreport,
        ToolboxItemKind.Gauge
    ];

    public IReportItem? Create(ToolboxItemKind kind, double leftPx, double topPx)
    {
        if (kind == ToolboxItemKind.Pointer)
            return null;
        return _edits.Insert(kind, leftPx, topPx);
    }
}

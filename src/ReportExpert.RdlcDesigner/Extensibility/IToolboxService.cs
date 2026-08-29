namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// Catalog of report items available from the toolbox, plus activation for click-to-insert.
/// </summary>
public interface IToolboxService
{
    IReadOnlyList<ToolboxItem> Items { get; }
    IReadOnlyList<string> Categories { get; }
    ToolboxItemKind? ActiveTool { get; }
    event EventHandler? ActiveToolChanged;
    event EventHandler<ToolboxItem>? ItemActivated;

    void SetActiveTool(ToolboxItemKind kind);
    ToolboxItem? Find(ToolboxItemKind kind);
}

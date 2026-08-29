namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// Default report toolbox catalog (supported items only).
/// </summary>
public sealed class ToolboxService : IToolboxService
{
    private readonly List<ToolboxItem> _items;
    private ToolboxItemKind _activeTool = ToolboxItemKind.Pointer;

    public ToolboxService()
    {
        // Pointer / Subreport / Gauge / Data Regions are intentionally omitted —
        // not fully functional in the current designer.
        _items =
        [
            Create(ToolboxItemKind.TextBox, "TextBox", "Report Items", "Display text, fields, or expressions."),
            Create(ToolboxItemKind.Rectangle, "Rectangle", "Report Items", "Container for grouping report items."),
            Create(ToolboxItemKind.Line, "Line", "Report Items", "Horizontal or diagonal line."),
            Create(ToolboxItemKind.Image, "Image", "Report Items", "External image from a local file.")
        ];
    }

    public IReadOnlyList<ToolboxItem> Items => _items;

    public IReadOnlyList<string> Categories =>
        _items.Select(i => i.Category).Distinct(StringComparer.Ordinal).ToList();

    public ToolboxItemKind? ActiveTool => _activeTool;

    public event EventHandler? ActiveToolChanged;
    public event EventHandler<ToolboxItem>? ItemActivated;

    public void SetActiveTool(ToolboxItemKind kind)
    {
        if (_activeTool == kind)
            return;
        _activeTool = kind;
        ActiveToolChanged?.Invoke(this, EventArgs.Empty);

        ToolboxItem? item = Find(kind);
        if (item is not null && item.CreatesItem)
            ItemActivated?.Invoke(this, item);
    }

    public ToolboxItem? Find(ToolboxItemKind kind) =>
        _items.FirstOrDefault(i => i.Kind == kind);

    private static ToolboxItem Create(ToolboxItemKind kind, string name, string category, string description) =>
        new(kind, name, category, iconResourceKey: null, description);
}

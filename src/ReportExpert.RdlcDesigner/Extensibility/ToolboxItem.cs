using System.Windows.Media;

namespace ReportExpert.RdlcDesigner.Extensibility;

/// <summary>
/// A single entry in the professional report-item toolbox.
/// </summary>
public sealed class ToolboxItem
{
    public ToolboxItem(
        ToolboxItemKind kind,
        string displayName,
        string category,
        string? iconResourceKey = null,
        string? description = null)
    {
        Kind = kind;
        DisplayName = displayName;
        Category = category;
        IconResourceKey = iconResourceKey;
        Description = description ?? displayName;
    }

    public ToolboxItemKind Kind { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }
    public string? IconResourceKey { get; }
    public ImageSource? Icon { get; set; }

    /// <summary>True when activating the item inserts a report item (false for Pointer).</summary>
    public bool CreatesItem => Kind != ToolboxItemKind.Pointer;
}

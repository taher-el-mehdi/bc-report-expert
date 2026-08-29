namespace ReportExpert.Domain.Models;

public sealed class ExplorerTreeItem
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public string? SampleValue { get; set; }
    public RdlcFieldInfo? Field { get; set; }
    public RdlcDataSetInfo? DataSet { get; set; }
    public List<ExplorerTreeItem> Children { get; set; } = [];
}

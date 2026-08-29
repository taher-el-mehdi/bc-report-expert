namespace ReportExpert.Domain.Models;

public sealed class RdlcDataSetInfo
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<RdlcFieldInfo> Fields { get; init; } = [];
}

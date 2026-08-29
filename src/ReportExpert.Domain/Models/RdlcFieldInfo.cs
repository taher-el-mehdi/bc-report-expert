namespace ReportExpert.Domain.Models;

public sealed class RdlcFieldInfo
{
    public string Name { get; init; } = string.Empty;
    public string DataField { get; init; } = string.Empty;
    public Type ClrType { get; init; } = typeof(string);
    public string TypeName { get; init; } = "System.String";
}

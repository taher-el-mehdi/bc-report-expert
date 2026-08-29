namespace ReportExpert.Modules.Preview.Models;

public sealed class RdlcParameterInfo
{
    public string Name { get; init; } = string.Empty;
    public string DataType { get; init; } = "String";
    public bool Nullable { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string> ValidValues { get; init; } = [];
    public bool AllowBlank { get; init; } = true;
    public bool MultiValue { get; init; }
}

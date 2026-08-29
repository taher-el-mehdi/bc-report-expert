namespace ReportExpert.RdlcDesigner.Abstractions;

public sealed record RdlcFieldNode(string Name, string DataField, string? TypeName);
public sealed record RdlcDataSetNode(string Name, IReadOnlyList<RdlcFieldNode> Fields);
public sealed record RdlcParameterNode(
    string Name,
    string DataType,
    bool Nullable,
    bool AllowBlank,
    string? Prompt,
    string? DefaultValue);

/// <summary>
/// Datasets, fields, and report parameters from the open RDLC.
/// </summary>
public interface ISchemaService
{
    IReadOnlyList<RdlcDataSetNode> DataSets { get; }
    IReadOnlyList<RdlcParameterNode> Parameters { get; }
    event EventHandler? SchemaChanged;
    void Refresh();
    RdlcParameterNode? AddParameter(string name);
    bool RemoveParameter(string name);
    void UpdateParameter(string name, string dataType, bool nullable, bool allowBlank, string? prompt, string? defaultValue);
}

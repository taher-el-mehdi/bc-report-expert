namespace ReportExpert.RdlcDesigner.Properties;

/// <summary>
/// One editable property exposed by a provider.
/// </summary>
public sealed class PropertyDefinition
{
    public PropertyDefinition(
        string key,
        string displayName,
        PropertyEditorKind editor,
        object? value,
        Action<object?> apply,
        bool isReadOnly = false,
        string? description = null)
    {
        Key = key;
        DisplayName = displayName;
        Editor = editor;
        Value = value;
        Apply = apply;
        IsReadOnly = isReadOnly;
        Description = description;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public PropertyEditorKind Editor { get; }
    public object? Value { get; }
    public Action<object?> Apply { get; }
    public bool IsReadOnly { get; }
    public string? Description { get; }
}

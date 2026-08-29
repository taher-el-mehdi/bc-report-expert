namespace ReportExpert.RdlcDesigner.Properties;

public sealed class PropertyCategory
{
    public PropertyCategory(string name, IReadOnlyList<PropertyDefinition> properties)
    {
        Name = name;
        Properties = properties;
    }

    public string Name { get; }
    public IReadOnlyList<PropertyDefinition> Properties { get; }
}

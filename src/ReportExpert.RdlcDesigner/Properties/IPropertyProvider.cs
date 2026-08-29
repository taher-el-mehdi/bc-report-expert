namespace ReportExpert.RdlcDesigner.Properties;

/// <summary>
/// Extensible provider that contributes categories/properties for a given selection.
/// </summary>
public interface IPropertyProvider
{
    /// <summary>Higher values win when multiple providers claim the same context.</summary>
    int Priority { get; }

    bool CanProvide(PropertyContext context);

    IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context);
}

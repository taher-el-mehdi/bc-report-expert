using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

/// <summary>Fallback for Other / unknown items.</summary>
public sealed class GenericItemPropertyProvider : IPropertyProvider
{
    public int Priority => 20;

    public bool CanProvide(PropertyContext context) =>
        context.Primary is not null && !context.IsMultiSelect;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, context.Properties);
        PropertyProviderHelpers.AddHidden(general, item, context.Properties);

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, context.Properties);

        var categories = new List<PropertyCategory>
        {
            PropertyProviderHelpers.Category(item.Kind.ToString(), general)
        };
        if (layout.Count > 0)
            categories.Add(PropertyProviderHelpers.Category("Layout", layout));
        return categories;
    }
}

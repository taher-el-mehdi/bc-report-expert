using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class LinePropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind == ReportItemKind.Line;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, context.Properties);
        PropertyProviderHelpers.AddHidden(general, item, context.Properties);

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, context.Properties);

        return
        [
            PropertyProviderHelpers.Category("Line", general),
            PropertyProviderHelpers.Category("Layout", layout)
        ];
    }
}

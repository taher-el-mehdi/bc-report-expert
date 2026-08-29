using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class GaugePropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind == ReportItemKind.Gauge;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, context.Properties);
        PropertyProviderHelpers.AddHidden(general, item, context.Properties);
        general.Add(new PropertyDefinition(
            "Hint", "Note", PropertyEditorKind.ReadOnlyLabel,
            "Gauge structure editing is limited; layout and visibility are available.",
            _ => { }, isReadOnly: true));

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, context.Properties);

        return
        [
            PropertyProviderHelpers.Category("Gauge", general),
            PropertyProviderHelpers.Category("Layout", layout)
        ];
    }
}

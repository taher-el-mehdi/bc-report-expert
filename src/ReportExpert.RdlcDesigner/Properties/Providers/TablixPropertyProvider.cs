using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class TablixPropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind == ReportItemKind.Tablix;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, context.Properties);
        PropertyProviderHelpers.AddHidden(general, item, context.Properties);
        general.Add(new PropertyDefinition(
            "Hint",
            "Note",
            PropertyEditorKind.ReadOnlyLabel,
            "Select a cell textbox to edit cell values.",
            _ => { },
            isReadOnly: true));

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, context.Properties);

        return
        [
            PropertyProviderHelpers.Category("Tablix", general),
            PropertyProviderHelpers.Category("Layout", layout)
        ];
    }
}

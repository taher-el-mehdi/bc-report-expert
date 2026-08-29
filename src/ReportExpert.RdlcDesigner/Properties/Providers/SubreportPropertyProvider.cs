using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

public sealed class SubreportPropertyProvider : IPropertyProvider
{
    public int Priority => 100;

    public bool CanProvide(PropertyContext context) =>
        context.Primary?.Kind == ReportItemKind.Subreport;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReportItem item = context.Primary!;
        var general = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddName(general, item, context.Properties);
        general.Add(new PropertyDefinition(
            "ReportName",
            "Report name",
            PropertyEditorKind.Text,
            PropertyProviderHelpers.ReadSubreportName(item) ?? string.Empty,
            v =>
            {
                PropertyProviderHelpers.WriteSubreportName(item, v?.ToString());
                context.Document?.NotifyChanged();
            }));
        PropertyProviderHelpers.AddHidden(general, item, context.Properties);

        var layout = new List<PropertyDefinition>();
        PropertyProviderHelpers.AddGeometry(layout, item, context.Properties);

        return
        [
            PropertyProviderHelpers.Category("Subreport", general),
            PropertyProviderHelpers.Category("Layout", layout)
        ];
    }
}

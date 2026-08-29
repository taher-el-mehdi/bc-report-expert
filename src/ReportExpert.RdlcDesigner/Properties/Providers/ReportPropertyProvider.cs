using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

/// <summary>No selection → report / page properties.</summary>
public sealed class ReportPropertyProvider : IPropertyProvider
{
    public int Priority => 10;

    public bool CanProvide(PropertyContext context) => context.IsEmpty && context.Document is not null;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        RdlcDocument doc = context.Document!;
        var general = new List<PropertyDefinition>
        {
            new("FilePath", "File", PropertyEditorKind.ReadOnlyLabel,
                string.IsNullOrWhiteSpace(doc.FilePath) ? "(unsaved)" : System.IO.Path.GetFileName(doc.FilePath),
                _ => { }, isReadOnly: true),
            new("ItemCount", "Items", PropertyEditorKind.ReadOnlyLabel,
                doc.Items.Count.ToString(), _ => { }, isReadOnly: true)
        };

        var layout = new List<PropertyDefinition>
        {
            PropertyProviderHelpers.Num("PageWidth", "Page width (px)", doc.PageWidthPx, _ => { }),
            PropertyProviderHelpers.Num("PageHeight", "Page height (px)", doc.PageHeightPx, _ => { })
        };
        // Page size is currently read-only at the property browser (structure-preserving).
        layout[0] = new PropertyDefinition("PageWidth", "Page width (px)", PropertyEditorKind.ReadOnlyLabel,
            doc.PageWidthPx.ToString("0.##"), _ => { }, isReadOnly: true);
        layout[1] = new PropertyDefinition("PageHeight", "Page height (px)", PropertyEditorKind.ReadOnlyLabel,
            doc.PageHeightPx.ToString("0.##"), _ => { }, isReadOnly: true);

        return
        [
            PropertyProviderHelpers.Category("Report", general),
            PropertyProviderHelpers.Category("Page", layout)
        ];
    }
}

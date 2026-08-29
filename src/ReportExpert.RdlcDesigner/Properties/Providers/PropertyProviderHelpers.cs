using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

internal static class PropertyProviderHelpers
{
    public static void AddName(List<PropertyDefinition> list, IReportItem item, IPropertyService props)
    {
        if (!item.CanEditName)
            return;
        list.Add(new PropertyDefinition(
            "Name",
            "Name",
            PropertyEditorKind.Text,
            item.Name,
            v => props.SetName(item, v?.ToString()?.Trim() ?? string.Empty)));
    }

    public static void AddGeometry(List<PropertyDefinition> list, IReportItem item, IPropertyService props)
    {
        if (!item.CanEditGeometry)
            return;

        list.Add(Num("Left", "Left", item.LeftPx, left =>
            props.SetGeometry(item, left, item.TopPx, item.WidthPx, item.HeightPx)));
        list.Add(Num("Top", "Top", item.TopPx, top =>
            props.SetGeometry(item, item.LeftPx, top, item.WidthPx, item.HeightPx)));
        list.Add(Num("Width", "Width", item.WidthPx, width =>
            props.SetGeometry(item, item.LeftPx, item.TopPx, width, item.HeightPx)));
        list.Add(Num("Height", "Height", item.HeightPx, height =>
            props.SetGeometry(item, item.LeftPx, item.TopPx, item.WidthPx, height)));
    }

    public static void AddHidden(List<PropertyDefinition> list, IReportItem item, IPropertyService props)
    {
        list.Add(new PropertyDefinition(
            "Hidden",
            "Hidden",
            PropertyEditorKind.Boolean,
            item.Hidden,
            v => props.SetHidden(item, v is true)));
    }

    public static PropertyDefinition Num(string key, string label, double value, Action<double> apply) =>
        new(key, label, PropertyEditorKind.Number, value, v =>
        {
            if (v is double d)
                apply(d);
            else if (v is not null && double.TryParse(v.ToString(), out double parsed))
                apply(parsed);
        });

    public static PropertyCategory Category(string name, params PropertyDefinition[] props) =>
        new(name, props);

    public static PropertyCategory Category(string name, IEnumerable<PropertyDefinition> props) =>
        new(name, props.ToList());

    public static string? ReadSubreportName(IReportItem item)
    {
        if (item is not ReportItemModel model)
            return null;
        return model.Element.Element(model.Namespace + "ReportName")?.Value;
    }

    public static void WriteSubreportName(IReportItem item, string? reportName, Action? notify = null)
    {
        if (item is not ReportItemModel model)
            return;
        var el = model.Element.Element(model.Namespace + "ReportName");
        if (el is null)
            model.Element.Add(new System.Xml.Linq.XElement(model.Namespace + "ReportName", reportName ?? string.Empty));
        else
            el.Value = reportName ?? string.Empty;
        model.RefreshFromXml();
        notify?.Invoke();
    }
}

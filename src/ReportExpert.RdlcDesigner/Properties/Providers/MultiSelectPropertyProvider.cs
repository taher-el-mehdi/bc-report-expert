using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Properties.Providers;

/// <summary>Multiple selection → only shared geometry / hidden properties.</summary>
public sealed class MultiSelectPropertyProvider : IPropertyProvider
{
    public int Priority => 200;

    public bool CanProvide(PropertyContext context) => context.IsMultiSelect;

    public IReadOnlyList<PropertyCategory> GetCategories(PropertyContext context)
    {
        IReadOnlyList<IReportItem> items = context.Selection;
        IPropertyService props = context.Properties;

        bool allGeometry = items.All(i => i.CanEditGeometry);
        bool allSameHidden = items.Select(i => i.Hidden).Distinct().Count() == 1;

        var shared = new List<PropertyDefinition>
        {
            new("Count", "Selection", PropertyEditorKind.ReadOnlyLabel,
                $"{items.Count} items", _ => { }, isReadOnly: true),
            new("Kinds", "Kinds", PropertyEditorKind.ReadOnlyLabel,
                string.Join(", ", items.Select(i => i.Kind).Distinct()),
                _ => { }, isReadOnly: true)
        };

        if (allSameHidden)
        {
            bool hidden = items[0].Hidden;
            shared.Add(new PropertyDefinition(
                "Hidden",
                "Hidden",
                PropertyEditorKind.Boolean,
                hidden,
                v =>
                {
                    bool value = v is true;
                    foreach (IReportItem item in items)
                        props.SetHidden(item, value);
                }));
        }

        var categories = new List<PropertyCategory>
        {
            PropertyProviderHelpers.Category("Shared", shared)
        };

        if (allGeometry)
        {
            double? left = SameOrNull(items.Select(i => i.LeftPx));
            double? top = SameOrNull(items.Select(i => i.TopPx));
            double? width = SameOrNull(items.Select(i => i.WidthPx));
            double? height = SameOrNull(items.Select(i => i.HeightPx));

            var layout = new List<PropertyDefinition>();
            if (left is not null)
            {
                layout.Add(PropertyProviderHelpers.Num("Left", "Left", left.Value, l =>
                {
                    foreach (IReportItem item in items)
                        props.SetGeometry(item, l, item.TopPx, item.WidthPx, item.HeightPx);
                }));
            }

            if (top is not null)
            {
                layout.Add(PropertyProviderHelpers.Num("Top", "Top", top.Value, t =>
                {
                    foreach (IReportItem item in items)
                        props.SetGeometry(item, item.LeftPx, t, item.WidthPx, item.HeightPx);
                }));
            }

            if (width is not null)
            {
                layout.Add(PropertyProviderHelpers.Num("Width", "Width", width.Value, w =>
                {
                    foreach (IReportItem item in items)
                        props.SetGeometry(item, item.LeftPx, item.TopPx, w, item.HeightPx);
                }));
            }

            if (height is not null)
            {
                layout.Add(PropertyProviderHelpers.Num("Height", "Height", height.Value, h =>
                {
                    foreach (IReportItem item in items)
                        props.SetGeometry(item, item.LeftPx, item.TopPx, item.WidthPx, h);
                }));
            }

            if (layout.Count > 0)
                categories.Add(PropertyProviderHelpers.Category("Layout", layout));
        }

        return categories;
    }

    private static double? SameOrNull(IEnumerable<double> values)
    {
        double[] arr = values.ToArray();
        if (arr.Length == 0)
            return null;
        double first = arr[0];
        return arr.All(v => Math.Abs(v - first) < 0.01) ? first : null;
    }
}

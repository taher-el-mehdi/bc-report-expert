using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Properties;

/// <summary>
/// Resolves the active property provider for the current selection.
/// </summary>
public sealed class PropertyBrowserService
{
    private readonly Func<RdlcDocument?> _document;
    private readonly ISelectionService _selection;
    private readonly IPropertyService _properties;
    private readonly List<IPropertyProvider> _providers;

    public PropertyBrowserService(
        Func<RdlcDocument?> document,
        ISelectionService selection,
        IPropertyService properties,
        IEnumerable<IPropertyProvider>? providers = null)
    {
        _document = document;
        _selection = selection;
        _properties = properties;
        _providers = (providers ?? CreateDefaultProviders()).OrderByDescending(p => p.Priority).ToList();
        _selection.SelectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<IPropertyProvider> Providers => _providers;

    public void Register(IPropertyProvider provider)
    {
        _providers.Add(provider);
        _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public PropertyContext CreateContext() =>
        new(_document(), _selection.Selected, _properties);

    public IReadOnlyList<PropertyCategory> GetCategories()
    {
        PropertyContext context = CreateContext();
        IPropertyProvider? provider = _providers.FirstOrDefault(p => p.CanProvide(context));
        return provider?.GetCategories(context) ?? Array.Empty<PropertyCategory>();
    }

    public string GetSelectionCaption()
    {
        PropertyContext context = CreateContext();
        if (context.IsEmpty)
            return "Report";
        if (context.IsMultiSelect)
            return $"{context.Selection.Count} items selected";
        IReportItem item = context.Primary!;
        return $"{item.Kind} — {item.Name}";
    }

    public static IEnumerable<IPropertyProvider> CreateDefaultProviders() =>
    [
        new Providers.MultiSelectPropertyProvider(),
        new Providers.TextBoxPropertyProvider(),
        new Providers.RectanglePropertyProvider(),
        new Providers.LinePropertyProvider(),
        new Providers.ImagePropertyProvider(),
        new Providers.TablixPropertyProvider(),
        new Providers.ChartPropertyProvider(),
        new Providers.SubreportPropertyProvider(),
        new Providers.GaugePropertyProvider(),
        new Providers.GenericItemPropertyProvider()
    ];
}

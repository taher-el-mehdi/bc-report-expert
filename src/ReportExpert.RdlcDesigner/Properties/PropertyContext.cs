using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Properties;

/// <summary>
/// Selection + document context passed to property providers.
/// </summary>
public sealed class PropertyContext
{
    public PropertyContext(
        RdlcDocument? document,
        IReadOnlyList<IReportItem> selection,
        IPropertyService properties)
    {
        Document = document;
        Selection = selection;
        Properties = properties;
    }

    public RdlcDocument? Document { get; }
    public IReadOnlyList<IReportItem> Selection { get; }
    public IPropertyService Properties { get; }

    public IReportItem? Primary => Selection.Count == 1 ? Selection[0] : null;
    public bool IsEmpty => Selection.Count == 0;
    public bool IsMultiSelect => Selection.Count > 1;
}

using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Model;

public sealed class RdlcDocument : IRdlcDocument
{
    private readonly List<ReportItemModel> _items = [];

    public RdlcDocument(XDocument xml, string filePath)
    {
        Xml = xml;
        FilePath = filePath;
        XElement report = xml.Root
            ?? throw new InvalidOperationException("Invalid RDLC: no root.");
        if (report.Name.LocalName != "Report")
            throw new InvalidOperationException("Invalid RDLC: no Report element.");

        Namespace = report.Name.Namespace;
        RebuildIndex();
    }

    public XDocument Xml { get; }
    public XNamespace Namespace { get; }
    public string? FilePath { get; set; }
    public double PageWidthPx { get; private set; }
    public double PageHeightPx { get; private set; }
    public IReadOnlyList<IReportItem> Items => _items;
    public IReadOnlyList<ReportItemModel> MutableItems => _items;

    public event EventHandler? Changed;

    public IReportItem? Find(string id) =>
        _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal));

    public ReportItemModel? FindModel(string id) =>
        _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal));

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public XElement GetOrCreateBodyReportItems()
    {
        XElement report = Xml.Root!;
        (XElement? body, _) = ResolveBodyAndPage(report, Namespace);
        if (body is null)
            throw new InvalidOperationException("RDLC has no Body.");

        XElement? items = body.Element(Namespace + "ReportItems");
        if (items is not null)
            return items;

        items = new XElement(Namespace + "ReportItems");
        body.AddFirst(items);
        return items;
    }

    public XElement GetOrCreateDataSets()
    {
        XElement report = Xml.Root!;
        XElement? dataSets = report.Element(Namespace + "DataSets");
        if (dataSets is not null)
            return dataSets;
        dataSets = new XElement(Namespace + "DataSets");
        report.Add(dataSets);
        return dataSets;
    }

    public XElement GetOrCreateParameters()
    {
        XElement report = Xml.Root!;
        XElement? parameters = report.Element(Namespace + "ReportParameters");
        if (parameters is not null)
            return parameters;
        parameters = new XElement(Namespace + "ReportParameters");
        report.Add(parameters);
        return parameters;
    }

    public void RebuildIndex()
    {
        _items.Clear();
        XElement report = Xml.Root!;
        (XElement? body, XElement? page) = ResolveBodyAndPage(report, Namespace);

        string? pageWidth = page?.Element(Namespace + "PageWidth")?.Value
            ?? report.Element(Namespace + "PageWidth")?.Value
            ?? "8.5in";
        string? pageHeight = page?.Element(Namespace + "PageHeight")?.Value
            ?? report.Element(Namespace + "PageHeight")?.Value
            ?? "11in";
        PageWidthPx = RdlUnits.ToPx(pageWidth);
        PageHeightPx = RdlUnits.ToPx(pageHeight);
        if (PageWidthPx <= 0)
            PageWidthPx = 8.5 * 96;
        if (PageHeightPx <= 0)
            PageHeightPx = 11 * 96;

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        if (body is not null)
            CollectItems(body.Element(Namespace + "ReportItems"), canEditGeometry: true, usedIds);

        if (page is not null)
        {
            CollectItems(page.Element(Namespace + "PageHeader")?.Element(Namespace + "ReportItems"), true, usedIds);
            CollectItems(page.Element(Namespace + "PageFooter")?.Element(Namespace + "ReportItems"), true, usedIds);
        }
    }

    private void CollectItems(XElement? reportItems, bool canEditGeometry, HashSet<string> usedIds)
    {
        if (reportItems is null)
            return;

        foreach (XElement child in reportItems.Elements())
            CollectItem(child, canEditGeometry, usedIds);
    }

    private void CollectItem(XElement element, bool canEditGeometry, HashSet<string> usedIds)
    {
        string local = element.Name.LocalName;
        ReportItemKind kind = local switch
        {
            "Textbox" => ReportItemKind.Textbox,
            "Rectangle" => ReportItemKind.Rectangle,
            "Tablix" => ReportItemKind.Tablix,
            "Line" => ReportItemKind.Line,
            "Image" => ReportItemKind.Image,
            "Chart" => ReportItemKind.Chart,
            "Subreport" => ReportItemKind.Subreport,
            "GaugePanel" or "Gauge" => ReportItemKind.Gauge,
            _ => ReportItemKind.Other
        };

        bool geometryEditable = canEditGeometry && kind is
            ReportItemKind.Textbox or ReportItemKind.Rectangle or ReportItemKind.Line
            or ReportItemKind.Image or ReportItemKind.Tablix or ReportItemKind.Chart
            or ReportItemKind.Subreport or ReportItemKind.Gauge;

        if (kind is not ReportItemKind.Other)
        {
            string id = AllocateId(element.Attribute("Name")?.Value, local, usedIds);
            _items.Add(new ReportItemModel(id, kind, element, Namespace, geometryEditable));
        }

        if (local == "Rectangle")
            CollectItems(element.Element(Namespace + "ReportItems"), canEditGeometry: true, usedIds);

        if (local == "Tablix")
            CollectTablixCells(element, usedIds);
    }

    private void CollectTablixCells(XElement tablix, HashSet<string> usedIds)
    {
        XElement? body = tablix.Element(Namespace + "TablixBody");
        if (body is null)
            return;

        string tablixName = tablix.Attribute("Name")?.Value ?? "Tablix";
        int rowIndex = 0;
        foreach (XElement row in body.Element(Namespace + "TablixRows")?.Elements(Namespace + "TablixRow") ?? [])
        {
            int colIndex = 0;
            foreach (XElement cell in row.Element(Namespace + "TablixCells")?.Elements(Namespace + "TablixCell") ?? [])
            {
                foreach (XElement textbox in EnumerateCellTextboxes(cell))
                {
                    if (_items.Any(i => ReferenceEquals(i.Element, textbox)))
                        continue;

                    string baseName = textbox.Attribute("Name")?.Value
                        ?? $"{tablixName}_r{rowIndex}c{colIndex}";
                    string id = AllocateId(baseName, "Textbox", usedIds);
                    _items.Add(new ReportItemModel(
                        id,
                        ReportItemKind.TablixCellTextbox,
                        textbox,
                        Namespace,
                        canEditGeometry: false));
                }

                colIndex++;
            }

            rowIndex++;
        }
    }

    private IEnumerable<XElement> EnumerateCellTextboxes(XElement cell)
    {
        XElement? contents = cell.Element(Namespace + "CellContents");
        if (contents is null)
            yield break;

        foreach (XElement child in contents.Elements())
        {
            if (child.Name.LocalName == "Textbox")
            {
                yield return child;
                continue;
            }

            if (child.Name.LocalName != "Rectangle")
                continue;

            foreach (XElement nested in child.Element(Namespace + "ReportItems")?.Elements() ?? [])
            {
                if (nested.Name.LocalName == "Textbox")
                    yield return nested;
            }
        }
    }

    public string AllocateUniqueName(string preferred)
    {
        var used = new HashSet<string>(
            Xml.Descendants().Select(e => e.Attribute("Name")?.Value).Where(n => !string.IsNullOrWhiteSpace(n))!,
            StringComparer.OrdinalIgnoreCase);
        string id = preferred;
        int n = 1;
        while (!used.Add(id))
            id = $"{preferred}{n++}";
        return id;
    }

    private static string AllocateId(string? preferred, string kind, HashSet<string> usedIds)
    {
        string baseId = string.IsNullOrWhiteSpace(preferred) ? kind : preferred.Trim();
        string id = baseId;
        int n = 1;
        while (!usedIds.Add(id))
            id = $"{baseId}_{n++}";
        return id;
    }

    internal static (XElement? Body, XElement? Page) ResolveBodyAndPage(XElement report, XNamespace ns)
    {
        XElement? sections = report.Element(ns + "ReportSections");
        XElement? section = sections?.Element(ns + "ReportSection");
        if (section is not null)
            return (section.Element(ns + "Body"), section.Element(ns + "Page"));

        return (report.Element(ns + "Body"), report.Element(ns + "Page"));
    }
}

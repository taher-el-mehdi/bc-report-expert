using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Reading;

/// <summary>
/// High-level, read-only views over a report definition.
/// </summary>
public static class ReportReader
{
    /// <summary>
    /// Summarizes a report: how many datasets, parameters and columns it has, and what each
    /// dataset queries.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="filePath">The path to report back, normally the one the document came from.</param>
    /// <returns>The summary.</returns>
    public static ReportDescription Describe(RdlDocument document, string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;

        var datasets = document
            .Descendants("DataSet")
            .Select(dataset =>
            {
                var query = dataset.Element(ns + "Query");

                return new DataSetBrief(
                    Name: dataset.Attribute("Name")?.Value ?? string.Empty,
                    CommandType: query is null
                        ? "Embedded"
                        : query.Element(ns + "CommandType")?.Value ?? "Unknown",
                    Command: query?.Element(ns + "CommandText")?.Value ?? string.Empty,
                    FieldCount: dataset.Descendants(ns + "Field").Count());
            })
            .ToList();

        int parameterCount = document.Descendants("ReportParameter").Count();

        var tablixes = TablixNavigator.AllTablixes(document);
        int columnCount = tablixes.Count == 0 ? 0 : TablixNavigator.Columns(tablixes[0], ns).Count;

        return new ReportDescription(
            new ReportSummary(datasets.Count, parameterCount, columnCount),
            datasets,
            filePath);
    }

    /// <summary>
    /// Lists every tablix in the report so that other tools can target one by name.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <returns>The tablixes, in document order.</returns>
    public static TablixesResult GetTablixes(RdlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;

        var tablixes = TablixNavigator
            .AllTablixes(document)
            .Select((tablix, index) => new TablixSummary(
                Index: index,
                Name: TablixNavigator.NameOf(tablix),
                DataSetName: tablix.Element(ns + "DataSetName")?.Value ?? string.Empty,
                ColumnCount: TablixNavigator.Columns(tablix, ns).Count,
                RowCount: TablixNavigator.Rows(tablix, ns).Count,
                Width: tablix.Element(ns + "Width")?.Value ?? string.Empty))
            .ToList();

        return new TablixesResult(tablixes);
    }

    /// <summary>
    /// Reads the page geometry of a report.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <returns>The page setup.</returns>
    /// <exception cref="RdlNotFoundException">The report has no <c>Page</c> element.</exception>
    public static PageSetup GetPageSetup(RdlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;

        var page = document.Descendants("Page").FirstOrDefault()
            ?? throw new RdlNotFoundException(
                RdlTarget.Page,
                "The report has no Page element, so it has no page geometry to read.");

        string Read(string name) => page.Element(ns + name)?.Value ?? string.Empty;

        string pageWidth = Read("PageWidth");
        string pageHeight = Read("PageHeight");
        string leftMargin = Read("LeftMargin");
        string rightMargin = Read("RightMargin");

        double widthInches = Dimension.ToInches(pageWidth);
        double heightInches = Dimension.ToInches(pageHeight);
        double usable = widthInches - Dimension.ToInches(leftMargin) - Dimension.ToInches(rightMargin);

        int columns = int.TryParse(Read("Columns"), out int parsed) ? parsed : 1;

        return new PageSetup
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            Orientation = widthInches > heightInches ? "Landscape" : "Portrait",
            LeftMargin = leftMargin,
            RightMargin = rightMargin,
            TopMargin = Read("TopMargin"),
            BottomMargin = Read("BottomMargin"),
            Columns = columns < 1 ? 1 : columns,
            UsableWidth = Dimension.FromInches(usable < 0 ? 0 : usable),
        };
    }

    /// <summary>
    /// Inventories the report items in the layout: textboxes, images, charts, subreports and
    /// nested data regions.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="kind">
    /// Restrict results to one RDL element name, for example <c>Textbox</c>. When
    /// <see langword="null"/> every known item kind is returned.
    /// </param>
    /// <returns>The matching items, in document order.</returns>
    public static ReportItemsResult GetReportItems(RdlDocument document, string? kind = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;

        string[] knownKinds =
        [
            "Textbox", "Image", "Rectangle", "Line", "Subreport",
            "Tablix", "Chart", "Gauge", "Map", "CustomReportItem",
        ];

        var wanted = kind is null
            ? knownKinds
            : knownKinds.Where(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (wanted.Length == 0)
        {
            throw new ArgumentException(
                $"'{kind}' is not a report item kind. Use one of: {string.Join(", ", knownKinds)}.",
                nameof(kind));
        }

        var wantedSet = new HashSet<string>(wanted, StringComparer.Ordinal);

        var items = document.Root
            .Descendants()
            .Where(element => element.Name.Namespace == ns && wantedSet.Contains(element.Name.LocalName))
            .Select(element => new ReportItemInfo(
                Name: element.Attribute("Name")?.Value ?? string.Empty,
                Kind: element.Name.LocalName,
                Path: BuildPath(element),
                Top: element.Element(ns + "Top")?.Value ?? string.Empty,
                Left: element.Element(ns + "Left")?.Value ?? string.Empty,
                Width: element.Element(ns + "Width")?.Value ?? string.Empty,
                Height: element.Element(ns + "Height")?.Value ?? string.Empty,
                Value: string.Equals(element.Name.LocalName, "Textbox", StringComparison.Ordinal)
                    ? element.Descendants(ns + "Value").FirstOrDefault()?.Value ?? string.Empty
                    : string.Empty,
                Hidden: element.Element(ns + "Visibility")?.Element(ns + "Hidden")?.Value ?? string.Empty))
            .ToList();

        return new ReportItemsResult(items.Count, items);
    }

    /// <summary>
    /// Builds a slash-separated trail of the named ancestors of an element, so an agent can tell
    /// which region an item lives in.
    /// </summary>
    private static string BuildPath(XElement element)
    {
        var segments = new List<string>();

        for (XElement? ancestor = element.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            string? name = ancestor.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(name))
                segments.Add(name);
        }

        segments.Reverse();
        return segments.Count == 0 ? "/" : "/" + string.Join('/', segments);
    }
}

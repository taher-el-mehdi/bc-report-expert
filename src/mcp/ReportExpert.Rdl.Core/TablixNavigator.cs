using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core;

/// <summary>
/// Navigation helpers for the tablix grid, shared by the readers and the editors.
/// </summary>
/// <remarks>
/// Every lookup walks named child elements rather than searching all descendants. A tablix can
/// contain another tablix inside a cell, and a descendant search would silently mix the two grids
/// together.
/// </remarks>
public static class TablixNavigator
{
    /// <summary>
    /// Returns every tablix in the report, in document order.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <returns>The tablix elements.</returns>
    public static IReadOnlyList<XElement> AllTablixes(RdlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return [.. document.Descendants("Tablix")];
    }

    /// <summary>
    /// Locates a tablix by name, or the first one in the report when no name is given.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="tablixName">
    /// The tablix name, or <see langword="null"/> to take the first tablix in document order.
    /// </param>
    /// <returns>The tablix element.</returns>
    /// <exception cref="RdlNotFoundException">
    /// The report has no tablix, or no tablix carries the requested name.
    /// </exception>
    public static XElement RequireTablix(RdlDocument document, string? tablixName = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var tablixes = AllTablixes(document);

        if (string.IsNullOrWhiteSpace(tablixName))
        {
            return tablixes.Count > 0
                ? tablixes[0]
                : throw new RdlNotFoundException(RdlTarget.Tablix, "No Tablix found in report");
        }

        foreach (var tablix in tablixes)
        {
            if (string.Equals(NameOf(tablix), tablixName, StringComparison.Ordinal))
                return tablix;
        }

        string available = tablixes.Count == 0
            ? "the report contains no tablixes"
            : $"available: {string.Join(", ", tablixes.Select(NameOf))}";

        throw new RdlNotFoundException(
            RdlTarget.Tablix,
            $"No Tablix named '{tablixName}' was found ({available}).");
    }

    /// <summary>Reads a tablix's <c>Name</c> attribute.</summary>
    /// <param name="tablix">The tablix element.</param>
    /// <returns>The name, or an empty string when unnamed.</returns>
    public static string NameOf(XElement tablix)
    {
        ArgumentNullException.ThrowIfNull(tablix);
        return tablix.Attribute("Name")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Returns the <c>TablixColumns</c> container that holds the column definitions.
    /// </summary>
    /// <param name="tablix">The tablix element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The container element.</returns>
    /// <exception cref="InvalidRdlException">The tablix has no column container.</exception>
    public static XElement RequireColumnsContainer(XElement tablix, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(tablix);

        return tablix.Element(ns + "TablixBody")?.Element(ns + "TablixColumns")
            ?? throw new InvalidRdlException(
                $"Tablix '{NameOf(tablix)}' has no TablixBody/TablixColumns element, so its columns cannot be read or changed.");
    }

    /// <summary>Returns the column definitions of a tablix, left to right.</summary>
    /// <param name="tablix">The tablix element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The <c>TablixColumn</c> elements.</returns>
    public static IReadOnlyList<XElement> Columns(XElement tablix, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(tablix);

        var container = tablix.Element(ns + "TablixBody")?.Element(ns + "TablixColumns");
        return container is null ? [] : [.. container.Elements(ns + "TablixColumn")];
    }

    /// <summary>Returns the body rows of a tablix, top to bottom.</summary>
    /// <param name="tablix">The tablix element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The <c>TablixRow</c> elements.</returns>
    public static IReadOnlyList<XElement> Rows(XElement tablix, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(tablix);

        var container = tablix.Element(ns + "TablixBody")?.Element(ns + "TablixRows");
        return container is null ? [] : [.. container.Elements(ns + "TablixRow")];
    }

    /// <summary>Returns the cells of a row, left to right.</summary>
    /// <param name="row">A <c>TablixRow</c> element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The <c>TablixCell</c> elements.</returns>
    public static IReadOnlyList<XElement> Cells(XElement row, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(row);

        var container = row.Element(ns + "TablixCells");
        return container is null ? [] : [.. container.Elements(ns + "TablixCell")];
    }

    /// <summary>
    /// Returns the <c>TablixMembers</c> element of the column hierarchy, which must stay in step
    /// with the column definitions.
    /// </summary>
    /// <param name="tablix">The tablix element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The container element, or <see langword="null"/> when the tablix has no column hierarchy.</returns>
    public static XElement? ColumnHierarchyMembers(XElement tablix, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(tablix);
        return tablix.Element(ns + "TablixColumnHierarchy")?.Element(ns + "TablixMembers");
    }

    /// <summary>
    /// Finds the first row playing a given role, or <see langword="null"/> when the tablix has none.
    /// </summary>
    /// <param name="tablix">The tablix element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <param name="kind">The role to look for.</param>
    /// <returns>The row element, or <see langword="null"/>.</returns>
    public static XElement? FindRow(XElement tablix, XNamespace ns, TablixRowKind kind)
    {
        foreach (var row in Rows(tablix, ns))
        {
            if (DetectRowKind(Cells(row, ns), ns) == kind)
                return row;
        }

        return null;
    }

    /// <summary>
    /// Infers what role a row plays from the content of its cells.
    /// </summary>
    /// <param name="cells">The row's cells.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The inferred role.</returns>
    /// <remarks>
    /// A cell counts as static text, an aggregate, or a plain binding. Aggregates win because a
    /// totals row usually also carries a static label; otherwise a majority of static text means a
    /// header and anything else is treated as the detail row.
    /// </remarks>
    public static TablixRowKind DetectRowKind(IReadOnlyList<XElement> cells, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(cells);

        string[] aggregateFunctions = ["Sum(", "Count(", "Avg(", "Min(", "Max(", "First("];

        int staticCount = 0;
        int bindingCount = 0;
        int aggregateCount = 0;

        foreach (var cell in cells)
        {
            string? text = FirstValueText(cell, ns)?.Trim();
            if (string.IsNullOrEmpty(text))
                continue;

            if (!text.StartsWith('='))
            {
                staticCount++;
            }
            else if (aggregateFunctions.Any(fn => text.Contains(fn, StringComparison.Ordinal)))
            {
                aggregateCount++;
            }
            else
            {
                bindingCount++;
            }
        }

        if (staticCount + bindingCount + aggregateCount == 0)
            return TablixRowKind.Empty;

        if (aggregateCount > 0 && aggregateCount >= bindingCount)
            return TablixRowKind.Footer;

        return staticCount > bindingCount ? TablixRowKind.Header : TablixRowKind.Data;
    }

    /// <summary>
    /// Returns the first textbox inside a cell, or <see langword="null"/> when the cell is empty.
    /// </summary>
    /// <param name="cell">A <c>TablixCell</c> element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The textbox element, or <see langword="null"/>.</returns>
    public static XElement? FirstTextbox(XElement cell, XNamespace ns)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Descendants(ns + "Textbox").FirstOrDefault();
    }

    /// <summary>
    /// Returns the text a cell displays, or <see langword="null"/> when it displays nothing.
    /// </summary>
    /// <param name="cell">A <c>TablixCell</c> element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The value text, or <see langword="null"/>.</returns>
    public static string? FirstValueText(XElement cell, XNamespace ns)
    {
        var textbox = FirstTextbox(cell, ns);
        return textbox?.Descendants(ns + "Value").FirstOrDefault()?.Value;
    }

    /// <summary>
    /// Recomputes a tablix's overall width from its column widths and writes it back.
    /// </summary>
    /// <param name="tablix">The tablix element.</param>
    /// <param name="ns">The report definition namespace.</param>
    /// <returns>The new total width in inches.</returns>
    /// <remarks>
    /// A tablix whose declared width disagrees with the sum of its columns renders with clipped or
    /// floating content, so this runs after every operation that adds, removes or resizes a column.
    /// </remarks>
    public static double RecalculateWidth(XElement tablix, XNamespace ns)
    {
        double total = 0d;

        foreach (var column in Columns(tablix, ns))
            total += Dimension.ToInches(column.Element(ns + "Width")?.Value);

        var widthElement = tablix.Element(ns + "Width");
        if (widthElement is not null)
            widthElement.Value = Dimension.FromInches(total);

        return total;
    }
}

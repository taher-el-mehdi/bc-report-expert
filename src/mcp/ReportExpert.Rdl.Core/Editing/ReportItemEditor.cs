using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Editing;

/// <summary>
/// Edits and removes free-standing report items addressed by name.
/// </summary>
/// <remarks>
/// Covers textboxes, images, rectangles, lines, charts and other named layout items. Tablix
/// columns stay under <see cref="ColumnEditor"/>; this is for whole items on the page.
/// </remarks>
public static class ReportItemEditor
{
    private static readonly HashSet<string> KnownKinds = new(StringComparer.Ordinal)
    {
        "Textbox", "Image", "Rectangle", "Line", "Subreport",
        "Tablix", "Chart", "Gauge", "Map", "CustomReportItem",
    };

    /// <summary>
    /// Shows or hides a named report item by writing its <c>Visibility/Hidden</c> element.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="itemName">The item's <c>Name</c> attribute.</param>
    /// <param name="hidden">
    /// <see langword="true"/> to hide, <see langword="false"/> to show, or an RDL expression
    /// such as <c>=Parameters!ShowLogo.Value = False</c>.
    /// </param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">No item carries that name.</exception>
    public static EditOutcome SetVisibility(RdlDocument document, string itemName, string hidden)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hidden);

        XNamespace ns = document.Ns;
        var item = RequireReportItem(document, itemName);
        string previous = ReadHidden(item, ns) ?? "false";

        var visibility = item.Element(ns + "Visibility");
        if (visibility is null)
        {
            visibility = new XElement(ns + "Visibility");
            item.Add(visibility);
        }

        var hiddenElement = visibility.Element(ns + "Hidden");
        if (hiddenElement is null)
        {
            hiddenElement = new XElement(ns + "Hidden");
            visibility.AddFirst(hiddenElement);
        }

        hiddenElement.Value = NormalizeHidden(hidden);

        string summary = hiddenElement.Value switch
        {
            "true" => $"Hid report item \"{itemName}\" ({item.Name.LocalName})",
            "false" => $"Showed report item \"{itemName}\" ({item.Name.LocalName})",
            _ => $"Set visibility of \"{itemName}\" ({item.Name.LocalName}) to {hiddenElement.Value}",
        };

        return new EditOutcome(summary)
        {
            Details = new Dictionary<string, object?>
            {
                ["item_name"] = itemName,
                ["kind"] = item.Name.LocalName,
                ["hidden"] = hiddenElement.Value,
                ["previous_hidden"] = previous,
            },
        };
    }

    /// <summary>
    /// Removes a named report item from the layout.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="itemName">The item's <c>Name</c> attribute.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">No item carries that name.</exception>
    /// <exception cref="InvalidRdlException">
    /// The item is a tablix still needed as structure, or removing it would leave a cell empty
    /// without a replacement — callers should prefer <see cref="SetVisibility"/> for cells.
    /// </exception>
    public static EditOutcome RemoveReportItem(RdlDocument document, string itemName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);

        var item = RequireReportItem(document, itemName);
        string kind = item.Name.LocalName;

        // CellContents must keep exactly one report item. Dropping the only child would leave an
        // empty cell that fails validation, so refuse and steer the caller to hide instead.
        if (item.Parent is { Name.LocalName: "CellContents" } cellContents &&
            cellContents.Elements().Count() == 1)
        {
            throw new InvalidRdlException(
                $"\"{itemName}\" is the only content of a tablix cell, so it cannot be removed. " +
                "Hide it with set_report_item_visibility instead, or clear it with set_textbox_value.");
        }

        item.Remove();

        return new EditOutcome($"Removed report item \"{itemName}\" ({kind})")
        {
            Details = new Dictionary<string, object?>
            {
                ["item_name"] = itemName,
                ["kind"] = kind,
            },
        };
    }

    /// <summary>
    /// Locates any known report item by name.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="itemName">The item's <c>Name</c> attribute.</param>
    /// <returns>The element.</returns>
    /// <exception cref="RdlNotFoundException">No item carries that name.</exception>
    public static XElement RequireReportItem(RdlDocument document, string itemName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);

        XNamespace ns = document.Ns;

        foreach (var element in document.Root.Descendants())
        {
            if (element.Name.Namespace != ns || !KnownKinds.Contains(element.Name.LocalName))
                continue;

            if (string.Equals(element.Attribute("Name")?.Value, itemName, StringComparison.Ordinal))
                return element;
        }

        throw new RdlNotFoundException(
            RdlTarget.ReportItem,
            $"No report item named \"{itemName}\" was found. " +
            "Call get_rdl_report_items to list the items in this report.");
    }

    /// <summary>Reads the current <c>Visibility/Hidden</c> value, or <see langword="null"/> when unset.</summary>
    internal static string? ReadHidden(XElement item, XNamespace ns) =>
        item.Element(ns + "Visibility")?.Element(ns + "Hidden")?.Value;

    private static string NormalizeHidden(string hidden)
    {
        if (string.Equals(hidden, "true", StringComparison.OrdinalIgnoreCase))
            return "true";

        if (string.Equals(hidden, "false", StringComparison.OrdinalIgnoreCase))
            return "false";

        // Expressions keep their leading '=' and whatever the caller wrote.
        return hidden.Trim();
    }

}

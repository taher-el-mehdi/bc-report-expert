using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Editing;

/// <summary>
/// Edits to the page geometry of a report definition.
/// </summary>
public static class PageEditor
{
    /// <summary>
    /// Applies page geometry changes.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="change">The changes to apply. Unset properties are left alone.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentException">Nothing was supplied, or a size or orientation is invalid.</exception>
    /// <exception cref="RdlNotFoundException">The report has no <c>Page</c> element.</exception>
    public static EditOutcome SetPageSetup(RdlDocument document, PageSetupChange change)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(change);

        if (change.IsEmpty)
        {
            throw new ArgumentException(
                "No page settings were supplied, so there is nothing to change.",
                nameof(change));
        }

        XNamespace ns = document.Ns;

        var page = document.Descendants("Page").FirstOrDefault()
            ?? throw new RdlNotFoundException(
                RdlTarget.Page,
                "The report has no Page element, so its page geometry cannot be changed.");

        var changes = new List<string>();

        Apply(page, ns, "PageWidth", change.PageWidth, nameof(change.PageWidth), changes);
        Apply(page, ns, "PageHeight", change.PageHeight, nameof(change.PageHeight), changes);
        Apply(page, ns, "LeftMargin", change.LeftMargin, nameof(change.LeftMargin), changes);
        Apply(page, ns, "RightMargin", change.RightMargin, nameof(change.RightMargin), changes);
        Apply(page, ns, "TopMargin", change.TopMargin, nameof(change.TopMargin), changes);
        Apply(page, ns, "BottomMargin", change.BottomMargin, nameof(change.BottomMargin), changes);

        if (change.Orientation is not null)
            ApplyOrientation(page, ns, change.Orientation, changes);

        if (changes.Count == 0)
            return new EditOutcome("Page setup already matched the requested values; nothing to do");

        return new EditOutcome($"Updated page setup: {string.Join(", ", changes)}");
    }

    /// <summary>
    /// Shrinks or grows the page width so that a data region of the given width fits between the
    /// horizontal margins.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="contentWidthInches">The width the page must accommodate, in inches.</param>
    /// <returns>
    /// The new page width in inches, or <see langword="null"/> when the report has no page width
    /// to adjust.
    /// </returns>
    public static double? FitPageWidthTo(RdlDocument document, double contentWidthInches)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;

        var page = document.Descendants("Page").FirstOrDefault();
        var pageWidth = page?.Element(ns + "PageWidth");
        if (page is null || pageWidth is null)
            return null;

        double left = Dimension.ToInches(page.Element(ns + "LeftMargin")?.Value);
        double right = Dimension.ToInches(page.Element(ns + "RightMargin")?.Value);

        double total = contentWidthInches + left + right;
        pageWidth.Value = Dimension.FromInches(total);

        return total;
    }

    private static void Apply(
        XElement page,
        XNamespace ns,
        string elementName,
        string? value,
        string parameterName,
        List<string> changes)
    {
        if (value is null)
            return;

        Dimension.Validate(value, parameterName);

        var element = page.Element(ns + elementName);
        if (element is null)
        {
            page.Add(new XElement(ns + elementName, value));
        }
        else
        {
            if (string.Equals(element.Value, value, StringComparison.Ordinal))
                return;

            element.Value = value;
        }

        changes.Add($"{elementName} to {value}");
    }

    /// <summary>
    /// Applies an orientation by swapping the page dimensions, since RDL has no orientation element.
    /// </summary>
    private static void ApplyOrientation(XElement page, XNamespace ns, string orientation, List<string> changes)
    {
        bool landscape = orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        bool portrait = orientation.Equals("Portrait", StringComparison.OrdinalIgnoreCase);

        if (!landscape && !portrait)
        {
            throw new ArgumentException(
                $"'{orientation}' is not a page orientation. Use \"Portrait\" or \"Landscape\".",
                nameof(orientation));
        }

        var widthElement = page.Element(ns + "PageWidth");
        var heightElement = page.Element(ns + "PageHeight");

        if (widthElement is null || heightElement is null)
        {
            throw new RdlNotFoundException(
                RdlTarget.Page,
                "The page has no PageWidth and PageHeight, so its orientation cannot be changed.");
        }

        double width = Dimension.ToInches(widthElement.Value);
        double height = Dimension.ToInches(heightElement.Value);

        bool alreadyLandscape = width > height;
        if (alreadyLandscape == landscape)
            return;

        (widthElement.Value, heightElement.Value) = (heightElement.Value, widthElement.Value);
        changes.Add($"orientation to {(landscape ? "Landscape" : "Portrait")}");
    }
}

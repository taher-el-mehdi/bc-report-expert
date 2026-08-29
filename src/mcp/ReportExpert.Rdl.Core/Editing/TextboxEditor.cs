using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Editing;

/// <summary>
/// Edits to individual textboxes, addressed by name.
/// </summary>
/// <remarks>
/// Column-level tools cover the common cases. This is the escape hatch for the rest: a title in
/// the page header, a label outside any tablix, or one of several cells that happen to share the
/// same text.
/// </remarks>
public static class TextboxEditor
{
    /// <summary>
    /// Sets the text or expression a named textbox displays.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="textboxName">The textbox's <c>Name</c> attribute.</param>
    /// <param name="value">
    /// The new content. A leading <c>=</c> makes it an expression; anything else is literal text.
    /// </param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">No textbox carries that name.</exception>
    public static EditOutcome SetTextboxValue(RdlDocument document, string textboxName, string value)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(textboxName);
        ArgumentNullException.ThrowIfNull(value);

        XNamespace ns = document.Ns;
        var textbox = RequireTextbox(document, textboxName);

        var valueElement = textbox.Descendants(ns + "Value").FirstOrDefault();

        if (valueElement is null)
        {
            // A textbox with no paragraph structure cannot display anything, so build the minimal
            // chain Reporting Services expects.
            var paragraphs = ColumnEditor.GetOrCreate(textbox, ns + "Paragraphs");
            var paragraph = ColumnEditor.GetOrCreate(paragraphs, ns + "Paragraph");
            var textRuns = ColumnEditor.GetOrCreate(paragraph, ns + "TextRuns");
            var textRun = ColumnEditor.GetOrCreate(textRuns, ns + "TextRun");
            valueElement = ColumnEditor.GetOrCreate(textRun, ns + "Value");
        }

        string previous = valueElement.Value;
        valueElement.Value = value;

        return new EditOutcome($"Set textbox \"{textboxName}\" to \"{value}\"")
        {
            Details = new Dictionary<string, object?> { ["previous_value"] = previous },
        };
    }

    /// <summary>
    /// Locates a textbox by name.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="textboxName">The textbox's <c>Name</c> attribute.</param>
    /// <returns>The <c>Textbox</c> element.</returns>
    /// <exception cref="RdlNotFoundException">No textbox carries that name.</exception>
    public static XElement RequireTextbox(RdlDocument document, string textboxName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(textboxName);

        foreach (var textbox in document.Descendants("Textbox"))
        {
            if (string.Equals(textbox.Attribute("Name")?.Value, textboxName, StringComparison.Ordinal))
                return textbox;
        }

        throw new RdlNotFoundException(
            RdlTarget.Textbox,
            $"No textbox named \"{textboxName}\" was found. " +
            "Call get_rdl_report_items with kind=\"Textbox\" to list the textboxes in this report.");
    }
}

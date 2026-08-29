using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;
using ReportExpert.Rdl.Core.Reading;

namespace ReportExpert.Rdl.Core.Editing;

/// <summary>
/// Edits to the parameters of a report definition.
/// </summary>
public static class ParameterEditor
{
    /// <summary>
    /// Adds a report parameter.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="dataType">The data type, for example <c>String</c>, <c>Integer</c> or <c>DateTime</c>.</param>
    /// <param name="prompt">The prompt shown to the user.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlRefusedException">A parameter with that name already exists.</exception>
    public static EditOutcome AddParameter(RdlDocument document, string name, string dataType, string prompt)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataType);
        ArgumentNullException.ThrowIfNull(prompt);

        XNamespace ns = document.Ns;

        var container = document.Root.Element(ns + "ReportParameters");
        if (container is null)
        {
            container = new XElement(ns + "ReportParameters");

            // ReportParameters conventionally follows DataSets in the document.
            var datasets = document.Root.Element(ns + "DataSets");
            if (datasets is not null)
                datasets.AddAfterSelf(container);
            else
                document.Root.AddFirst(container);
        }

        bool exists = container
            .Elements(ns + "ReportParameter")
            .Any(p => string.Equals(p.Attribute("Name")?.Value, name, StringComparison.Ordinal));

        if (exists)
        {
            throw new RdlRefusedException(
                $"Parameter \"{name}\" already exists.",
                "Use update_parameter to change the existing parameter, or choose a different name.");
        }

        container.Add(new XElement(
            ns + "ReportParameter",
            new XAttribute("Name", name),
            new XElement(ns + "DataType", dataType),
            new XElement(ns + "Prompt", prompt)));

        return new EditOutcome($"Added parameter \"{name}\"");
    }

    /// <summary>
    /// Updates the prompt or default value of an existing report parameter.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="name">The parameter to update.</param>
    /// <param name="prompt">The new prompt, or <see langword="null"/> to leave it unchanged.</param>
    /// <param name="defaultValue">The new default value, or <see langword="null"/> to leave it unchanged.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The parameter does not exist.</exception>
    /// <exception cref="ArgumentException">Neither a prompt nor a default value was supplied.</exception>
    public static EditOutcome UpdateParameter(
        RdlDocument document,
        string name,
        string? prompt = null,
        string? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (prompt is null && defaultValue is null)
        {
            throw new ArgumentException(
                "No changes specified. Supply a prompt, a default value, or both.",
                nameof(prompt));
        }

        XNamespace ns = document.Ns;
        var parameter = ParameterReader.RequireParameter(document, name);

        var changes = new List<string>();

        if (prompt is not null)
        {
            ColumnEditor.GetOrCreate(parameter, ns + "Prompt").Value = prompt;
            changes.Add($"prompt to \"{prompt}\"");
        }

        if (defaultValue is not null)
        {
            var defaults = ColumnEditor.GetOrCreate(parameter, ns + "DefaultValue");
            var values = ColumnEditor.GetOrCreate(defaults, ns + "Values");
            ColumnEditor.GetOrCreate(values, ns + "Value").Value = defaultValue;
            changes.Add($"default value to \"{defaultValue}\"");
        }

        return new EditOutcome($"Updated parameter \"{name}\": {string.Join(", ", changes)}");
    }

    /// <summary>
    /// Removes a report parameter.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="name">The parameter to remove.</param>
    /// <param name="force">
    /// Remove the parameter even when expressions or dataset queries still reference it. Off by
    /// default because doing so leaves the report unrenderable.
    /// </param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The parameter does not exist.</exception>
    /// <exception cref="RdlRefusedException">The parameter is still referenced and <paramref name="force"/> is off.</exception>
    public static EditOutcome RemoveParameter(RdlDocument document, string name, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var parameter = ParameterReader.RequireParameter(document, name);
        var references = FindReferences(document, name, parameter);

        if (references.Count > 0 && !force)
        {
            throw new RdlRefusedException(
                $"Parameter \"{name}\" is still referenced in {references.Count} place(s): " +
                $"{string.Join("; ", references.Take(5))}.",
                "Remove or rewrite those expressions first, or pass force=true to remove it anyway.");
        }

        parameter.Remove();

        return new EditOutcome($"Removed parameter \"{name}\"")
        {
            Details = new Dictionary<string, object?> { ["orphaned_references"] = references },
        };
    }

    /// <summary>
    /// Finds expressions elsewhere in the report that mention a parameter.
    /// </summary>
    private static List<string> FindReferences(RdlDocument document, string name, XElement parameter)
    {
        string marker = $"Parameters!{name}";

        return document.Root
            .Descendants()
            // Leaf elements only: an ancestor's Value concatenates its children and would report
            // the same reference once per enclosing level.
            .Where(element => !element.HasElements)
            .Where(element => !element.Ancestors().Contains(parameter))
            .Select(element => element.Value)
            .Where(text => text.Contains(marker, StringComparison.Ordinal))
            .Select(text => text.Length > 80 ? text[..80] + "..." : text)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

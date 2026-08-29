using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;
using ReportExpert.Rdl.Core.Reading;

namespace ReportExpert.Rdl.Core.Editing;

/// <summary>
/// Edits to the datasets of a report definition.
/// </summary>
public static class DataSetEditor
{
    /// <summary>
    /// Replaces the command text of a dataset's query, typically to point it at a different
    /// stored procedure.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The dataset to change.</param>
    /// <param name="newCommandText">The new command text.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The dataset does not exist or has no query.</exception>
    public static EditOutcome UpdateStoredProcedure(RdlDocument document, string dataSetName, string newCommandText)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);
        ArgumentNullException.ThrowIfNull(newCommandText);

        XNamespace ns = document.Ns;
        var dataset = DataSetReader.RequireDataSet(document, dataSetName);

        var query = dataset.Element(ns + "Query")
            ?? throw new RdlNotFoundException(
                RdlTarget.DataSet,
                $"Dataset \"{dataSetName}\" has no Query element, so it has no command text to update. " +
                "It is an embedded dataset supplied by the host application.");

        ColumnEditor.GetOrCreate(query, ns + "CommandText").Value = newCommandText;

        return new EditOutcome($"Updated stored procedure to \"{newCommandText}\"");
    }

    /// <summary>
    /// Adds a field to a dataset.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The dataset to add to.</param>
    /// <param name="fieldName">The field name, as referenced by <c>Fields!Name.Value</c>.</param>
    /// <param name="dataField">The underlying column or property name in the data source.</param>
    /// <param name="typeName">The CLR type name, for example <c>System.String</c>.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The dataset does not exist.</exception>
    /// <exception cref="RdlRefusedException">The dataset already declares a field with that name.</exception>
    public static EditOutcome AddField(
        RdlDocument document,
        string dataSetName,
        string fieldName,
        string dataField,
        string typeName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(dataField);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        XNamespace ns = document.Ns;
        var dataset = DataSetReader.RequireDataSet(document, dataSetName);

        var fields = dataset.Element(ns + "Fields");
        if (fields is null)
        {
            fields = new XElement(ns + "Fields");

            // RDL requires Fields to follow Query, so insert rather than append when a query exists.
            var query = dataset.Element(ns + "Query");
            if (query is not null)
                query.AddAfterSelf(fields);
            else
                dataset.AddFirst(fields);
        }

        bool exists = fields
            .Elements(ns + "Field")
            .Any(f => string.Equals(f.Attribute("Name")?.Value, fieldName, StringComparison.Ordinal));

        if (exists)
        {
            throw new RdlRefusedException(
                $"Field \"{fieldName}\" already exists in dataset \"{dataSetName}\".",
                "Remove the existing field first, or choose a different field name.");
        }

        fields.Add(new XElement(
            ns + "Field",
            new XAttribute("Name", fieldName),
            new XElement(ns + "DataField", dataField),
            new XElement(RdlDocument.Rd + "TypeName", typeName)));

        return new EditOutcome($"Added field \"{fieldName}\" to dataset \"{dataSetName}\"");
    }

    /// <summary>
    /// Removes a field from a dataset.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The dataset to remove from.</param>
    /// <param name="fieldName">The field to remove.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The dataset or the field does not exist.</exception>
    /// <remarks>
    /// Expressions that reference the field are left untouched and will fail validation afterwards.
    /// Check with a field usage search before removing a field that is in use.
    /// </remarks>
    public static EditOutcome RemoveField(RdlDocument document, string dataSetName, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        XNamespace ns = document.Ns;
        var dataset = DataSetReader.RequireDataSet(document, dataSetName);

        var fields = dataset.Element(ns + "Fields")
            ?? throw new RdlNotFoundException(
                RdlTarget.Field,
                $"Dataset \"{dataSetName}\" has no fields.");

        var field = fields
            .Elements(ns + "Field")
            .FirstOrDefault(f => string.Equals(f.Attribute("Name")?.Value, fieldName, StringComparison.Ordinal))
            ?? throw new RdlNotFoundException(
                RdlTarget.Field,
                $"Field \"{fieldName}\" not found in dataset \"{dataSetName}\".");

        field.Remove();

        return new EditOutcome($"Removed field \"{fieldName}\" from dataset \"{dataSetName}\"");
    }

    /// <summary>
    /// Renames a field, optionally rewriting every expression that references it.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The dataset that owns the field.</param>
    /// <param name="fieldName">The current field name.</param>
    /// <param name="newFieldName">The new field name.</param>
    /// <param name="updateReferences">
    /// Whether to rewrite <c>Fields!Old</c> to <c>Fields!New</c> throughout the report.
    /// </param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The dataset or the field does not exist.</exception>
    /// <exception cref="RdlRefusedException">The new name is already taken.</exception>
    public static EditOutcome RenameField(
        RdlDocument document,
        string dataSetName,
        string fieldName,
        string newFieldName,
        bool updateReferences = true)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newFieldName);

        XNamespace ns = document.Ns;
        var dataset = DataSetReader.RequireDataSet(document, dataSetName);

        var fields = dataset.Element(ns + "Fields")
            ?? throw new RdlNotFoundException(RdlTarget.Field, $"Dataset \"{dataSetName}\" has no fields.");

        var field = fields
            .Elements(ns + "Field")
            .FirstOrDefault(f => string.Equals(f.Attribute("Name")?.Value, fieldName, StringComparison.Ordinal))
            ?? throw new RdlNotFoundException(
                RdlTarget.Field,
                $"Field \"{fieldName}\" not found in dataset \"{dataSetName}\".");

        bool taken = fields
            .Elements(ns + "Field")
            .Any(f => string.Equals(f.Attribute("Name")?.Value, newFieldName, StringComparison.Ordinal));

        if (taken)
        {
            throw new RdlRefusedException(
                $"Dataset \"{dataSetName}\" already has a field named \"{newFieldName}\".",
                "Choose a different name, or remove the existing field first.");
        }

        field.SetAttributeValue("Name", newFieldName);

        int rewritten = 0;
        if (updateReferences)
        {
            string marker = $"Fields!{fieldName}";

            foreach (var value in document.Descendants("Value"))
            {
                string text = value.Value;
                if (!text.StartsWith('=') || !text.Contains(marker, StringComparison.Ordinal))
                    continue;

                string replaced = ReplaceFieldToken(text, fieldName, newFieldName);
                if (string.Equals(replaced, text, StringComparison.Ordinal))
                    continue;

                value.Value = replaced;
                rewritten++;
            }
        }

        string suffix = updateReferences ? $" and updated {rewritten} expression(s)" : string.Empty;

        return new EditOutcome($"Renamed field \"{fieldName}\" to \"{newFieldName}\"{suffix}")
        {
            Details = new Dictionary<string, object?> { ["expressions_updated"] = rewritten },
        };
    }

    /// <summary>
    /// Adds a dataset to the report.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The new dataset's name.</param>
    /// <param name="commandText">The query command text, or <see langword="null"/> for an embedded dataset.</param>
    /// <param name="commandType">The query command type, for example <c>StoredProcedure</c> or <c>Text</c>.</param>
    /// <param name="dataSourceName">The data source the query runs against.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlRefusedException">A dataset with that name already exists.</exception>
    public static EditOutcome AddDataSet(
        RdlDocument document,
        string dataSetName,
        string? commandText = null,
        string? commandType = null,
        string? dataSourceName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);

        XNamespace ns = document.Ns;

        bool exists = document
            .Descendants("DataSet")
            .Any(d => string.Equals(d.Attribute("Name")?.Value, dataSetName, StringComparison.Ordinal));

        if (exists)
        {
            throw new RdlRefusedException(
                $"A dataset named \"{dataSetName}\" already exists.",
                "Choose a different name, or update the existing dataset instead of adding one.");
        }

        var container = document.Root.Element(ns + "DataSets");
        if (container is null)
        {
            container = new XElement(ns + "DataSets");
            document.Root.Add(container);
        }

        var dataset = new XElement(ns + "DataSet", new XAttribute("Name", dataSetName));

        if (commandText is not null || commandType is not null || dataSourceName is not null)
        {
            var query = new XElement(ns + "Query");

            if (dataSourceName is not null)
                query.Add(new XElement(ns + "DataSourceName", dataSourceName));

            if (commandType is not null)
                query.Add(new XElement(ns + "CommandType", commandType));

            query.Add(new XElement(ns + "CommandText", commandText ?? string.Empty));
            dataset.Add(query);
        }

        dataset.Add(new XElement(ns + "Fields"));
        container.Add(dataset);

        return new EditOutcome($"Added dataset \"{dataSetName}\"");
    }

    /// <summary>
    /// Removes a dataset from the report.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The dataset to remove.</param>
    /// <param name="force">
    /// Remove the dataset even when a data region is bound to it. Off by default because doing so
    /// leaves the report unrenderable.
    /// </param>
    /// <returns>What changed.</returns>
    /// <exception cref="RdlNotFoundException">The dataset does not exist.</exception>
    /// <exception cref="RdlRefusedException">A data region is bound to the dataset and <paramref name="force"/> is off.</exception>
    public static EditOutcome RemoveDataSet(RdlDocument document, string dataSetName, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);

        XNamespace ns = document.Ns;
        var dataset = DataSetReader.RequireDataSet(document, dataSetName);

        var boundRegions = document.Root
            .Descendants(ns + "DataSetName")
            .Where(e => string.Equals(e.Value, dataSetName, StringComparison.Ordinal))
            .Select(e => e.Parent?.Attribute("Name")?.Value ?? "(unnamed)")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (boundRegions.Count > 0 && !force)
        {
            throw new RdlRefusedException(
                $"Dataset \"{dataSetName}\" is still used by: {string.Join(", ", boundRegions)}.",
                "Rebind those data regions to another dataset first, or pass force=true to remove it anyway.");
        }

        dataset.Remove();

        return new EditOutcome($"Removed dataset \"{dataSetName}\"")
        {
            Details = new Dictionary<string, object?> { ["orphaned_regions"] = boundRegions },
        };
    }

    /// <summary>
    /// Rewrites <c>Fields!Old</c> to <c>Fields!New</c> without touching a longer name that merely
    /// starts with the old one.
    /// </summary>
    private static string ReplaceFieldToken(string expression, string oldName, string newName)
    {
        string marker = $"Fields!{oldName}";
        var builder = new System.Text.StringBuilder(expression.Length);

        int position = 0;
        while (true)
        {
            int found = expression.IndexOf(marker, position, StringComparison.Ordinal);
            if (found < 0)
            {
                builder.Append(expression, position, expression.Length - position);
                break;
            }

            int after = found + marker.Length;
            bool isWholeToken = after >= expression.Length
                || (!char.IsLetterOrDigit(expression[after]) && expression[after] != '_');

            builder.Append(expression, position, found - position);
            builder.Append(isWholeToken ? $"Fields!{newName}" : marker);
            position = after;
        }

        return builder.ToString();
    }
}

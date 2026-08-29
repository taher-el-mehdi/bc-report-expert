using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;
using ReportExpert.Rdl.Core.Reading;

namespace ReportExpert.Rdl.Core.Validation;

/// <summary>
/// Structural and semantic validation of a report definition.
/// </summary>
/// <remarks>
/// This checks what a report definition means, not whether it matches the RDL schema. The two
/// failures that actually break reports in practice are a data region bound to a dataset that does
/// not exist, and an expression referencing a field that its dataset does not declare. Both are
/// invisible to schema validation because both are well-formed XML.
/// </remarks>
public static class RdlValidator
{
    private const int MaxExpressionLengthInMessage = 100;
    private const int MaxAvailableFieldsInMessage = 10;
    private const int MaxTextboxAncestorSearchDepth = 10;

    /// <summary>
    /// Validates a report definition.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <returns>The issues and warnings found, if any.</returns>
    public static ValidationResult Validate(RdlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;
        var issues = new List<string>();
        var warnings = new List<string>();

        var datasets = document.Descendants("DataSet").ToList();
        if (datasets.Count == 0)
            return ValidationResult.Fail(["No datasets found"]);

        foreach (var dataset in datasets)
        {
            string name = dataset.Attribute("Name")?.Value ?? "Unknown";
            bool hasQuery = dataset.Element(ns + "Query") is not null;
            bool hasFields = dataset.Descendants(ns + "Field").Any();

            if (!hasQuery && !hasFields)
                issues.Add($"Dataset \"{name}\" has no Query element and no Fields");
        }

        var fieldsByDataSet = DataSetReader.BuildFieldMap(document);

        var tablixes = TablixNavigator.AllTablixes(document);
        if (tablixes.Count == 0)
            issues.Add("No Tablix (table) found");

        foreach (var tablix in tablixes)
            ValidateTablix(tablix, ns, fieldsByDataSet, issues, warnings);

        return issues.Count > 0
            ? ValidationResult.Fail(issues, warnings)
            : ValidationResult.Pass(warnings);
    }

    private static void ValidateTablix(
        XElement tablix,
        XNamespace ns,
        IReadOnlyDictionary<string, IReadOnlySet<string>> fieldsByDataSet,
        List<string> issues,
        List<string> warnings)
    {
        string tablixName = tablix.Attribute("Name")?.Value ?? "Unknown";
        string? dataSetName = tablix.Element(ns + "DataSetName")?.Value;

        if (string.IsNullOrEmpty(dataSetName))
        {
            warnings.Add($"Tablix \"{tablixName}\" has no DataSetName specified");
            return;
        }

        if (!fieldsByDataSet.ContainsKey(dataSetName))
        {
            issues.Add($"Tablix \"{tablixName}\" references unknown dataset \"{dataSetName}\"");
            return;
        }

        var invalidReferences = new List<InvalidReference>();

        void Check(string? expression, string location)
        {
            if (string.IsNullOrEmpty(expression))
                return;

            var byDataSet = ExpressionParser.ExtractWithContext(expression, dataSetName);

            foreach ((string referencedDataSet, var fields) in byDataSet)
            {
                bool dataSetExists = fieldsByDataSet.TryGetValue(referencedDataSet, out var known);

                foreach (string field in fields)
                {
                    if (!dataSetExists)
                    {
                        invalidReferences.Add(new InvalidReference(
                            field, referencedDataSet, location,
                            $"references unknown dataset \"{referencedDataSet}\""));
                    }
                    else if (!known!.Contains(field))
                    {
                        invalidReferences.Add(new InvalidReference(
                            field, referencedDataSet, location, Error: null));
                    }
                }
            }
        }

        foreach (var groupExpression in tablix.Descendants(ns + "GroupExpression"))
            Check(groupExpression.Value, "GroupExpression");

        // Sort expressions are Value elements too, so they are recorded here and skipped in the
        // sweep below to avoid reporting the same reference under two different locations.
        var sortValues = tablix
            .Descendants(ns + "SortExpression")
            .Elements(ns + "Value")
            .ToHashSet();

        foreach (var sortValue in sortValues)
            Check(sortValue.Value, "SortExpression");

        foreach (var value in tablix.Descendants(ns + "Value"))
        {
            if (sortValues.Contains(value))
                continue;

            string text = value.Value;
            if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith('='))
                continue;

            Check(text, FindTextboxName(value, ns) ?? "unknown location");
        }

        foreach (var reference in Deduplicate(invalidReferences))
            issues.Add(Describe(reference, tablixName, fieldsByDataSet));
    }

    /// <summary>
    /// Collapses references to the same field in the same dataset, which would otherwise be
    /// reported once per cell that uses it.
    /// </summary>
    private static IEnumerable<InvalidReference> Deduplicate(IEnumerable<InvalidReference> references)
    {
        var seen = new HashSet<(string Field, string DataSet)>();

        foreach (var reference in references)
        {
            if (seen.Add((reference.Field, reference.DataSet)))
                yield return reference;
        }
    }

    private static string Describe(
        InvalidReference reference,
        string tablixName,
        IReadOnlyDictionary<string, IReadOnlySet<string>> fieldsByDataSet)
    {
        string location = string.IsNullOrEmpty(reference.Location) ? "unknown location" : reference.Location;

        if (reference.Error is not null)
        {
            return $"Tablix \"{tablixName}\": Expression {reference.Error} " +
                   $"(field \"{reference.Field}\" in {location})";
        }

        string available;
        if (fieldsByDataSet.TryGetValue(reference.DataSet, out var known))
        {
            string listed = string.Join(", ", known.Order(StringComparer.Ordinal).Take(MaxAvailableFieldsInMessage));
            string ellipsis = known.Count > MaxAvailableFieldsInMessage ? "..." : string.Empty;
            available = $"Available fields: {listed}{ellipsis}";
        }
        else
        {
            available = $"Dataset \"{reference.DataSet}\" does not exist";
        }

        return $"Tablix \"{tablixName}\": Field \"{reference.Field}\" not found in dataset \"{reference.DataSet}\" " +
               $"(referenced in {location}). {available}";
    }

    /// <summary>
    /// Walks up from a value element looking for the textbox that contains it, so that an issue can
    /// name a place the user recognises rather than an XML path.
    /// </summary>
    private static string? FindTextboxName(XElement value, XNamespace ns)
    {
        int depth = 0;

        for (XElement? ancestor = value.Parent;
             ancestor is not null && depth < MaxTextboxAncestorSearchDepth;
             ancestor = ancestor.Parent, depth++)
        {
            if (ancestor.Name == ns + "Textbox")
                return ancestor.Attribute("Name")?.Value;
        }

        return null;
    }

    private sealed record InvalidReference(
        string Field,
        string DataSet,
        string Location,
        string? Error);

    /// <summary>
    /// Finds everywhere a dataset field is referenced across the whole report.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="fieldName">The field name to search for, without the <c>Fields!</c> prefix.</param>
    /// <param name="dataSetName">
    /// Restrict results to references resolving against this dataset. When <see langword="null"/>
    /// every dataset is considered.
    /// </param>
    /// <returns>The references found.</returns>
    /// <remarks>
    /// Answers the question that always precedes a rename or a delete: what breaks if this field
    /// goes away.
    /// </remarks>
    public static FieldUsagesResult FindFieldUsages(RdlDocument document, string fieldName, string? dataSetName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        XNamespace ns = document.Ns;
        var usages = new List<FieldUsage>();

        foreach (var tablix in TablixNavigator.AllTablixes(document))
        {
            string scope = tablix.Element(ns + "DataSetName")?.Value ?? string.Empty;

            foreach (var value in tablix.Descendants(ns + "Value"))
            {
                string text = value.Value;
                if (string.IsNullOrEmpty(text) || !text.TrimStart().StartsWith('='))
                    continue;

                var byDataSet = ExpressionParser.ExtractWithContext(text, scope);

                foreach ((string referencedDataSet, var fields) in byDataSet)
                {
                    if (dataSetName is not null &&
                        !string.Equals(referencedDataSet, dataSetName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!fields.Contains(fieldName, StringComparer.Ordinal))
                        continue;

                    usages.Add(new FieldUsage(
                        referencedDataSet,
                        FindTextboxName(value, ns) ?? $"Tablix {TablixNavigator.NameOf(tablix)}",
                        Truncate(text)));
                }
            }
        }

        return new FieldUsagesResult(fieldName, usages.Count, usages);
    }

    private static string Truncate(string expression) =>
        expression.Length <= MaxExpressionLengthInMessage
            ? expression
            : expression[..MaxExpressionLengthInMessage] + "...";
}

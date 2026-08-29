using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Core.Reading;

/// <summary>
/// Read-only access to the datasets in a report definition.
/// </summary>
public static class DataSetReader
{
    /// <summary>
    /// Reads every dataset, optionally including its fields.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="fieldLimit">
    /// How many fields to include per dataset: <c>0</c> for counts only, <c>-1</c> for all fields,
    /// or a positive number to take that many.
    /// </param>
    /// <param name="fieldPattern">
    /// An optional case-insensitive regular expression that field names must match. Applied before
    /// <paramref name="fieldLimit"/>.
    /// </param>
    /// <returns>The datasets, in document order.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="fieldLimit"/> is below <c>-1</c>, or <paramref name="fieldPattern"/> is not
    /// a valid regular expression.
    /// </exception>
    /// <remarks>
    /// Field counts are always reported in full even when the field list is filtered or truncated,
    /// so a caller can tell that there is more to fetch.
    /// </remarks>
    public static DataSetsResult GetDataSets(RdlDocument document, int fieldLimit = 0, string? fieldPattern = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (fieldLimit < -1)
        {
            throw new ArgumentException(
                $"field_limit must be -1 (all fields), 0 (counts only) or a positive number, but was {fieldLimit}.",
                nameof(fieldLimit));
        }

        Regex? filter = CompileFieldPattern(fieldPattern);
        XNamespace ns = document.Ns;

        var datasets = new List<DataSetDetail>();

        foreach (var dataset in document.Descendants("DataSet"))
        {
            var query = dataset.Element(ns + "Query");

            string commandType = query is null ? "Embedded" : query.Element(ns + "CommandType")?.Value ?? "Unknown";
            string commandText = query?.Element(ns + "CommandText")?.Value ?? string.Empty;
            string dataSource = query?.Element(ns + "DataSourceName")?.Value ?? string.Empty;

            var queryParameters = query is null
                ? []
                : query.Descendants(ns + "QueryParameter")
                    .Select(qp => new QueryParameterInfo(
                        qp.Attribute("Name")?.Value ?? string.Empty,
                        qp.Element(ns + "Value")?.Value ?? string.Empty))
                    .ToList();

            var allFields = dataset
                .Descendants(ns + "Field")
                .Select(field => new FieldInfo(
                    Name: field.Attribute("Name")?.Value ?? string.Empty,
                    DataField: field.Element(ns + "DataField")?.Value ?? string.Empty,
                    Type: field.Descendants(RdlDocument.Rd + "TypeName").FirstOrDefault()?.Value ?? "Unknown"))
                .ToList();

            IReadOnlyList<FieldInfo>? fields = null;
            bool? truncated = null;

            if (fieldLimit != 0)
            {
                var selected = filter is null
                    ? allFields
                    : allFields.Where(f => filter.IsMatch(f.Name)).ToList();

                if (fieldLimit == -1)
                {
                    fields = selected;
                    truncated = false;
                }
                else
                {
                    fields = selected.Take(fieldLimit).ToList();
                    truncated = selected.Count > fieldLimit;
                }
            }

            datasets.Add(new DataSetDetail
            {
                Name = dataset.Attribute("Name")?.Value ?? string.Empty,
                DataSource = dataSource,
                CommandType = commandType,
                CommandText = commandText,
                QueryParameters = queryParameters,
                FieldCount = allFields.Count,
                Fields = fields,
                FieldsTruncated = truncated,
            });
        }

        return new DataSetsResult(datasets);
    }

    /// <summary>
    /// Locates a dataset by name.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <param name="dataSetName">The dataset name.</param>
    /// <returns>The <c>DataSet</c> element.</returns>
    /// <exception cref="RdlNotFoundException">No dataset carries that name.</exception>
    public static XElement RequireDataSet(RdlDocument document, string dataSetName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetName);

        var datasets = document.Descendants("DataSet").ToList();

        foreach (var dataset in datasets)
        {
            if (string.Equals(dataset.Attribute("Name")?.Value, dataSetName, StringComparison.Ordinal))
                return dataset;
        }

        string available = datasets.Count == 0
            ? "the report has no datasets"
            : $"available: {string.Join(", ", datasets.Select(d => d.Attribute("Name")?.Value ?? "(unnamed)"))}";

        throw new RdlNotFoundException(
            RdlTarget.DataSet,
            $"Dataset \"{dataSetName}\" not found ({available}).");
    }

    /// <summary>
    /// Builds a lookup of dataset name to the set of field names it declares.
    /// </summary>
    /// <param name="document">The report definition.</param>
    /// <returns>The lookup, keyed by dataset name.</returns>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildFieldMap(RdlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        XNamespace ns = document.Ns;
        var map = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        foreach (var dataset in document.Descendants("DataSet"))
        {
            string name = dataset.Attribute("Name")?.Value ?? "Unknown";

            var fields = dataset
                .Descendants(ns + "Field")
                .Select(f => f.Attribute("Name")?.Value)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .ToHashSet(StringComparer.Ordinal);

            map[name] = fields;
        }

        return map;
    }

    private static Regex? CompileFieldPattern(string? fieldPattern)
    {
        if (string.IsNullOrWhiteSpace(fieldPattern))
            return null;

        try
        {
            return new Regex(fieldPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(
                $"field_pattern '{fieldPattern}' is not a valid regular expression: {ex.Message}",
                nameof(fieldPattern),
                ex);
        }
    }
}

using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// Counts of the top-level structures in a report.
/// </summary>
/// <param name="DataSets">Number of <c>DataSet</c> elements.</param>
/// <param name="Parameters">Number of <c>ReportParameter</c> elements.</param>
/// <param name="TableColumns">Number of columns in the first tablix.</param>
public sealed record ReportSummary(
    [property: JsonPropertyName("datasets")] int DataSets,
    [property: JsonPropertyName("parameters")] int Parameters,
    [property: JsonPropertyName("table_columns")] int TableColumns);

/// <summary>
/// A dataset reduced to the fields that matter when scanning a report for the first time.
/// </summary>
/// <param name="Name">The dataset name.</param>
/// <param name="CommandType">The query command type, or <c>Embedded</c> when the dataset has no query.</param>
/// <param name="Command">The query command text, empty when there is none.</param>
/// <param name="FieldCount">Number of fields the dataset declares.</param>
public sealed record DataSetBrief(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("command_type")] string CommandType,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("field_count")] int FieldCount);

/// <summary>
/// A high-level description of a report definition.
/// </summary>
/// <param name="ReportSummary">Structure counts.</param>
/// <param name="DataSets">One entry per dataset.</param>
/// <param name="FilePath">The path the report was read from.</param>
public sealed record ReportDescription(
    [property: JsonPropertyName("report_summary")] ReportSummary ReportSummary,
    [property: JsonPropertyName("datasets")] IReadOnlyList<DataSetBrief> DataSets,
    [property: JsonPropertyName("filepath")] string FilePath);

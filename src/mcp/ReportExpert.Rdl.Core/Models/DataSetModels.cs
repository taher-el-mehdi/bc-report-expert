using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// A field declared by a dataset.
/// </summary>
/// <param name="Name">The field name as referenced by <c>Fields!Name.Value</c> expressions.</param>
/// <param name="DataField">The underlying column or property name in the data source.</param>
/// <param name="Type">The CLR type name from <c>rd:TypeName</c>, or <c>Unknown</c> when absent.</param>
public sealed record FieldInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("data_field")] string DataField,
    [property: JsonPropertyName("type")] string Type);

/// <summary>
/// A parameter passed from the report to the dataset query.
/// </summary>
/// <param name="Name">The query parameter name.</param>
/// <param name="Value">The bound value, usually a <c>=Parameters!X.Value</c> expression.</param>
public sealed record QueryParameterInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);

/// <summary>
/// Full detail for a single dataset.
/// </summary>
public sealed record DataSetDetail
{
    /// <summary>The dataset name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The data source the query runs against, empty when the dataset has no query.</summary>
    [JsonPropertyName("datasource")]
    public required string DataSource { get; init; }

    /// <summary>The query command type, or <c>Embedded</c> when the dataset has no query.</summary>
    [JsonPropertyName("command_type")]
    public required string CommandType { get; init; }

    /// <summary>The query command text, empty when there is none.</summary>
    [JsonPropertyName("command_text")]
    public required string CommandText { get; init; }

    /// <summary>Parameters passed into the query.</summary>
    [JsonPropertyName("query_parameters")]
    public required IReadOnlyList<QueryParameterInfo> QueryParameters { get; init; }

    /// <summary>Total number of fields, regardless of any filter applied to <see cref="Fields"/>.</summary>
    [JsonPropertyName("field_count")]
    public required int FieldCount { get; init; }

    /// <summary>
    /// The fields themselves. Omitted entirely when the caller asked for counts only, which keeps
    /// the response small for reports with hundreds of fields per dataset.
    /// </summary>
    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FieldInfo>? Fields { get; init; }

    /// <summary>
    /// Whether <see cref="Fields"/> was cut short by the requested limit. Omitted when fields
    /// were not requested.
    /// </summary>
    [JsonPropertyName("fields_truncated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FieldsTruncated { get; init; }
}

/// <summary>
/// The datasets in a report.
/// </summary>
/// <param name="DataSets">One entry per dataset, in document order.</param>
public sealed record DataSetsResult(
    [property: JsonPropertyName("datasets")] IReadOnlyList<DataSetDetail> DataSets);

using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// One entry in a parameter's static list of allowed values.
/// </summary>
/// <param name="Value">The stored value.</param>
/// <param name="Label">The text shown to the user, falling back to the value when no label is set.</param>
public sealed record ValidValue(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label);

/// <summary>
/// A report parameter.
/// </summary>
public sealed record ParameterInfo
{
    /// <summary>The parameter name as referenced by <c>Parameters!Name.Value</c>.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The declared data type, for example <c>String</c> or <c>DateTime</c>.</summary>
    [JsonPropertyName("data_type")]
    public required string DataType { get; init; }

    /// <summary>The prompt shown to the user, empty when the parameter is hidden.</summary>
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    /// <summary>Default values, omitted when the parameter has none.</summary>
    [JsonPropertyName("default_values")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? DefaultValues { get; init; }

    /// <summary>
    /// The dataset supplying the allowed values, omitted unless the parameter is driven by a query.
    /// </summary>
    [JsonPropertyName("valid_values_dataset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValidValuesDataSet { get; init; }

    /// <summary>A static list of allowed values, omitted when the parameter has none.</summary>
    [JsonPropertyName("valid_values")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ValidValue>? ValidValues { get; init; }
}

/// <summary>
/// The parameters in a report.
/// </summary>
/// <param name="Parameters">One entry per parameter, in document order.</param>
public sealed record ParametersResult(
    [property: JsonPropertyName("parameters")] IReadOnlyList<ParameterInfo> Parameters);

using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// The outcome of validating a report definition.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>Whether the report passed with no issues.</summary>
    [JsonPropertyName("valid")]
    public required bool Valid { get; init; }

    /// <summary>
    /// Problems that make the report incorrect, omitted when there are none.
    /// </summary>
    [JsonPropertyName("issues")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Issues { get; init; }

    /// <summary>A confirmation message, present only when the report is valid.</summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>
    /// Observations that do not make the report invalid, omitted when there are none.
    /// </summary>
    [JsonPropertyName("warnings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }

    /// <summary>Creates a passing result.</summary>
    /// <param name="warnings">Non-fatal observations, if any.</param>
    /// <returns>A valid result.</returns>
    public static ValidationResult Pass(IReadOnlyList<string>? warnings = null) => new()
    {
        Valid = true,
        Message = "RDL structure is valid",
        Warnings = warnings is { Count: > 0 } ? warnings : null,
    };

    /// <summary>Creates a failing result.</summary>
    /// <param name="issues">The problems found. Must not be empty.</param>
    /// <param name="warnings">Non-fatal observations, if any.</param>
    /// <returns>An invalid result.</returns>
    public static ValidationResult Fail(IReadOnlyList<string> issues, IReadOnlyList<string>? warnings = null) => new()
    {
        Valid = false,
        Issues = issues,
        Warnings = warnings is { Count: > 0 } ? warnings : null,
    };
}

/// <summary>
/// One place a dataset field is referenced.
/// </summary>
/// <param name="DataSet">The dataset the reference resolves against.</param>
/// <param name="Location">The containing textbox name, or the kind of expression when there is no textbox.</param>
/// <param name="Expression">The expression text, truncated when very long.</param>
public sealed record FieldUsage(
    [property: JsonPropertyName("dataset")] string DataSet,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("expression")] string Expression);

/// <summary>
/// Everywhere a field is referenced in a report.
/// </summary>
/// <param name="FieldName">The field that was searched for.</param>
/// <param name="Count">Number of references found.</param>
/// <param name="Usages">The references themselves.</param>
public sealed record FieldUsagesResult(
    [property: JsonPropertyName("field_name")] string FieldName,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("usages")] IReadOnlyList<FieldUsage> Usages);

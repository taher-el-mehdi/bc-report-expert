using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// The role a tablix row plays, inferred from the kind of content in its cells.
/// </summary>
/// <remarks>
/// RDL does not label rows, so the role is a heuristic: static text implies a header, aggregate
/// expressions imply a footer, and plain field bindings imply the detail row.
/// </remarks>
public enum TablixRowKind
{
    /// <summary>No cell in the row has any text.</summary>
    Empty,

    /// <summary>Mostly static text: a column header row.</summary>
    Header,

    /// <summary>Mostly plain field bindings: the detail row.</summary>
    Data,

    /// <summary>Contains aggregate expressions: a totals row.</summary>
    Footer,
}

/// <summary>
/// A single tablix column, combining its definition with the header and detail cells above and
/// below it.
/// </summary>
public sealed record ColumnInfo
{
    /// <summary>Zero-based position of the column within the tablix.</summary>
    [JsonPropertyName("index")]
    public required int Index { get; init; }

    /// <summary>
    /// The header text. When the header cell holds a field expression, the field name is shown
    /// instead of the raw expression.
    /// </summary>
    [JsonPropertyName("header")]
    public required string Header { get; init; }

    /// <summary>The column width as an RDL size string, for example <c>"1.5in"</c>.</summary>
    [JsonPropertyName("width")]
    public required string Width { get; init; }

    /// <summary>The name of the textbox in the header cell, empty when there is none.</summary>
    [JsonPropertyName("textbox_name")]
    public required string TextboxName { get; init; }

    /// <summary>The detail cell expression, omitted when the column has no detail row.</summary>
    [JsonPropertyName("field_binding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FieldBinding { get; init; }

    /// <summary>
    /// The field name parsed out of <see cref="FieldBinding"/>, omitted when the binding is not a
    /// simple field reference.
    /// </summary>
    [JsonPropertyName("field_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FieldName { get; init; }

    /// <summary>The detail cell format string, omitted when none is set.</summary>
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; init; }
}

/// <summary>
/// The columns of a tablix.
/// </summary>
public sealed record ColumnsResult
{
    /// <summary>One entry per column, left to right.</summary>
    [JsonPropertyName("columns")]
    public required IReadOnlyList<ColumnInfo> Columns { get; init; }

    /// <summary>
    /// Why the column list is empty, omitted when columns were read successfully.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

/// <summary>
/// A tablix and the shape of its grid.
/// </summary>
/// <param name="Index">Zero-based position among the tablixes in the report, in document order.</param>
/// <param name="Name">The tablix name, used to target it in other tools.</param>
/// <param name="DataSetName">The dataset the tablix is bound to, empty when unbound.</param>
/// <param name="ColumnCount">Number of columns.</param>
/// <param name="RowCount">Number of rows in the tablix body.</param>
/// <param name="Width">The declared overall width, empty when not set.</param>
public sealed record TablixSummary(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("dataset_name")] string DataSetName,
    [property: JsonPropertyName("column_count")] int ColumnCount,
    [property: JsonPropertyName("row_count")] int RowCount,
    [property: JsonPropertyName("width")] string Width);

/// <summary>
/// Every tablix in a report.
/// </summary>
/// <param name="Tablixes">One entry per tablix, in document order.</param>
public sealed record TablixesResult(
    [property: JsonPropertyName("tablixes")] IReadOnlyList<TablixSummary> Tablixes);

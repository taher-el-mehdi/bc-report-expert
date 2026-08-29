using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// Page geometry for a report.
/// </summary>
public sealed record PageSetup
{
    /// <summary>Page width as an RDL size string, empty when not set.</summary>
    [JsonPropertyName("page_width")]
    public required string PageWidth { get; init; }

    /// <summary>Page height as an RDL size string, empty when not set.</summary>
    [JsonPropertyName("page_height")]
    public required string PageHeight { get; init; }

    /// <summary>
    /// <c>Landscape</c> when the page is wider than it is tall, otherwise <c>Portrait</c>.
    /// </summary>
    /// <remarks>Derived from the dimensions; RDL has no orientation element.</remarks>
    [JsonPropertyName("orientation")]
    public required string Orientation { get; init; }

    /// <summary>Left margin as an RDL size string.</summary>
    [JsonPropertyName("left_margin")]
    public required string LeftMargin { get; init; }

    /// <summary>Right margin as an RDL size string.</summary>
    [JsonPropertyName("right_margin")]
    public required string RightMargin { get; init; }

    /// <summary>Top margin as an RDL size string.</summary>
    [JsonPropertyName("top_margin")]
    public required string TopMargin { get; init; }

    /// <summary>Bottom margin as an RDL size string.</summary>
    [JsonPropertyName("bottom_margin")]
    public required string BottomMargin { get; init; }

    /// <summary>Number of newspaper-style columns the page is divided into.</summary>
    [JsonPropertyName("columns")]
    public required int Columns { get; init; }

    /// <summary>
    /// The usable width, that is the page width less both horizontal margins, as an RDL size string.
    /// </summary>
    /// <remarks>
    /// Provided because it is what actually constrains a tablix, and computing it correctly
    /// requires unit conversion the caller should not have to repeat.
    /// </remarks>
    [JsonPropertyName("usable_width")]
    public required string UsableWidth { get; init; }
}

/// <summary>
/// A report item such as a textbox, image, chart or subreport.
/// </summary>
/// <param name="Name">The item name.</param>
/// <param name="Kind">The RDL element name, for example <c>Textbox</c> or <c>Tablix</c>.</param>
/// <param name="Path">A slash-separated path of ancestor item names, locating the item in the layout.</param>
/// <param name="Top">The <c>Top</c> position, empty when the item is inside a cell and has none.</param>
/// <param name="Left">The <c>Left</c> position, empty when the item is inside a cell and has none.</param>
/// <param name="Width">The declared width, empty when not set.</param>
/// <param name="Height">The declared height, empty when not set.</param>
/// <param name="Value">
/// For textboxes, the first value or expression the item displays. Empty for other kinds.
/// </param>
/// <param name="Hidden">
/// The <c>Visibility/Hidden</c> value: <c>true</c>, <c>false</c>, an expression, or empty when unset.
/// </param>
public sealed record ReportItemInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("top")] string Top,
    [property: JsonPropertyName("left")] string Left,
    [property: JsonPropertyName("width")] string Width,
    [property: JsonPropertyName("height")] string Height,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("hidden")] string Hidden = "");

/// <summary>
/// The report items in a report.
/// </summary>
/// <param name="Count">Number of items found.</param>
/// <param name="Items">The items, in document order.</param>
public sealed record ReportItemsResult(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] IReadOnlyList<ReportItemInfo> Items);

/// <summary>
/// A backup taken before a report was modified.
/// </summary>
/// <param name="FileName">The backup file name.</param>
/// <param name="FilePath">Absolute path to the backup.</param>
/// <param name="CreatedUtc">When the backup was taken, in UTC.</param>
/// <param name="SizeBytes">Size of the backup in bytes.</param>
public sealed record BackupInfo(
    [property: JsonPropertyName("file_name")] string FileName,
    [property: JsonPropertyName("file_path")] string FilePath,
    [property: JsonPropertyName("created_utc")] DateTime CreatedUtc,
    [property: JsonPropertyName("size_bytes")] long SizeBytes);

/// <summary>
/// The backups available for a report.
/// </summary>
/// <param name="Count">Number of backups.</param>
/// <param name="Backups">The backups, newest first.</param>
public sealed record BackupsResult(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("backups")] IReadOnlyList<BackupInfo> Backups);

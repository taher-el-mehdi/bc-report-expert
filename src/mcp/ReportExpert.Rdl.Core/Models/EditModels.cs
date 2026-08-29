namespace ReportExpert.Rdl.Core.Models;

/// <summary>
/// The result of a successful edit.
/// </summary>
/// <remarks>
/// Edits that cannot be performed throw instead of returning an unsuccessful outcome, so that the
/// MCP tool layer can report them as errors with an actionable hint. An instance of this type
/// therefore always represents success.
/// </remarks>
/// <param name="Message">A human-readable description of what changed.</param>
public sealed record EditOutcome(string Message)
{
    /// <summary>
    /// Extra facts about the edit, such as the index a new column landed at. Merged into the tool
    /// response alongside the message.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Details { get; init; }
}

/// <summary>
/// Which row of a tablix an edit applies to.
/// </summary>
public enum ColumnRowTarget
{
    /// <summary>The header row.</summary>
    Header,

    /// <summary>The detail row.</summary>
    Data,

    /// <summary>The totals row.</summary>
    Footer,

    /// <summary>Every row in the column.</summary>
    All,
}

/// <summary>
/// Visual styling to apply to tablix cells. Every property is optional; a <see langword="null"/>
/// leaves the corresponding RDL element untouched rather than clearing it.
/// </summary>
public sealed record CellStyle
{
    /// <summary>Font family name, for example <c>"Segoe UI"</c>.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Font size as an RDL size string, for example <c>"10pt"</c>.</summary>
    public string? FontSize { get; init; }

    /// <summary>Font weight, for example <c>Normal</c> or <c>Bold</c>.</summary>
    public string? FontWeight { get; init; }

    /// <summary>Font style, for example <c>Normal</c> or <c>Italic</c>.</summary>
    public string? FontStyle { get; init; }

    /// <summary>Text colour, for example <c>"Black"</c> or <c>"#333333"</c>.</summary>
    public string? Color { get; init; }

    /// <summary>Cell background colour.</summary>
    public string? BackgroundColor { get; init; }

    /// <summary>Horizontal alignment: <c>Left</c>, <c>Center</c>, <c>Right</c> or <c>General</c>.</summary>
    public string? TextAlign { get; init; }

    /// <summary>Vertical alignment: <c>Top</c>, <c>Middle</c> or <c>Bottom</c>.</summary>
    public string? VerticalAlign { get; init; }

    /// <summary>Format string applied to the cell value, for example <c>"C2"</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Whether every property is unset, meaning there is nothing to apply.</summary>
    public bool IsEmpty =>
        FontFamily is null && FontSize is null && FontWeight is null && FontStyle is null &&
        Color is null && BackgroundColor is null && TextAlign is null && VerticalAlign is null &&
        Format is null;
}

/// <summary>
/// Page geometry to apply. Every property is optional; a <see langword="null"/> leaves the
/// existing value in place.
/// </summary>
public sealed record PageSetupChange
{
    /// <summary>New page width as an RDL size string.</summary>
    public string? PageWidth { get; init; }

    /// <summary>New page height as an RDL size string.</summary>
    public string? PageHeight { get; init; }

    /// <summary>
    /// <c>Portrait</c> or <c>Landscape</c>. Applied by swapping width and height when the current
    /// geometry does not already match.
    /// </summary>
    public string? Orientation { get; init; }

    /// <summary>New left margin as an RDL size string.</summary>
    public string? LeftMargin { get; init; }

    /// <summary>New right margin as an RDL size string.</summary>
    public string? RightMargin { get; init; }

    /// <summary>New top margin as an RDL size string.</summary>
    public string? TopMargin { get; init; }

    /// <summary>New bottom margin as an RDL size string.</summary>
    public string? BottomMargin { get; init; }

    /// <summary>Whether every property is unset, meaning there is nothing to apply.</summary>
    public bool IsEmpty =>
        PageWidth is null && PageHeight is null && Orientation is null &&
        LeftMargin is null && RightMargin is null && TopMargin is null && BottomMargin is null;
}

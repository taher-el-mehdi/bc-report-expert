using System.ComponentModel;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Core.Editing;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Mcp.Server.Tools;

/// <summary>
/// Tools that change the columns of a table.
/// </summary>
[McpServerToolType]
public static class ColumnTools
{
    /// <summary>Renames a column header.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="old_header">The current header text, matched exactly.</param>
    /// <param name="new_header">The replacement text.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "update_column_header", Destructive = true)]
    [Description(
        "Rename a column header in an RDLC report by matching its current text exactly, including case " +
        "and spacing. Only the first match in the report is changed. When several cells share the same " +
        "text, use set_textbox_value instead to target one precisely.")]
    public static ToolResponse UpdateColumnHeader(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("The header text as it appears today, matched exactly.")] string old_header,
        [Description("The new header text.")] string new_header,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ColumnEditor.UpdateColumnHeader(document, old_header, new_header));

    /// <summary>Changes a column's width.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="column_index">Zero-based index of the column.</param>
    /// <param name="new_width">The new width as an RDL size string.</param>
    /// <param name="tablix_name">The table to change.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "update_column_width", Destructive = true)]
    [Description(
        "Set the width of a column in an RDLC report table. The table's overall width is recalculated " +
        "to match the sum of its columns, which is required for the report to render correctly.")]
    public static ToolResponse UpdateColumnWidth(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Zero-based column index, as reported by get_rdl_columns.")] int column_index,
        [Description("The new width, a number with a unit suffix such as \"2.5in\", \"3cm\" or \"72pt\".")] string new_width,
        [Description("Which table to change. Omit to use the first table in the report.")] string? tablix_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ColumnEditor.UpdateColumnWidth(document, column_index, new_width, tablix_name));

    /// <summary>Sets a column's number or date format.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="column_index">Zero-based index of the column.</param>
    /// <param name="format_string">A .NET format string.</param>
    /// <param name="tablix_name">The table to change.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "update_column_format", Destructive = true)]
    [Description(
        "Set the format string applied to a column's detail cell in an RDLC report, for example \"C2\" " +
        "for currency with two decimals, \"N0\" for a whole number, or \"dd/MM/yyyy\" for a date. " +
        "Affects the detail row only, not the header.")]
    public static ToolResponse UpdateColumnFormat(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Zero-based column index, as reported by get_rdl_columns.")] int column_index,
        [Description("A .NET format string, for example \"C2\", \"N0\", \"P1\" or \"dd/MM/yyyy\".")] string format_string,
        [Description("Which table to change. Omit to use the first table in the report.")] string? tablix_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ColumnEditor.UpdateColumnFormat(document, column_index, format_string, tablix_name));

    /// <summary>Inserts a column.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="column_index">Where to insert, or -1 to append.</param>
    /// <param name="header_text">Text for the header cell.</param>
    /// <param name="field_binding">Expression for the detail cell.</param>
    /// <param name="width">Column width.</param>
    /// <param name="format_string">Optional format for the detail cell.</param>
    /// <param name="footer_expression">Optional expression for the totals cell.</param>
    /// <param name="tablix_name">The table to change.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "add_column", Destructive = true)]
    [Description(
        "Insert a new column into an RDLC report table. A cell is created in every row: the header row " +
        "gets header_text, the detail row gets field_binding, and a totals row, if the table has one, " +
        "gets footer_expression. The field named in field_binding must already exist in the table's " +
        "dataset; add it first with add_dataset_field if it does not.")]
    public static ToolResponse AddColumn(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Zero-based position to insert at, from 0 to the current column count. Pass -1 to append at the end.")]
        int column_index,
        [Description("Static text for the header cell, for example \"Amount\".")] string header_text,
        [Description("Expression for the detail cell, for example \"=Fields!Amount.Value\".")] string field_binding,
        [Description("Column width with a unit suffix, for example \"1in\".")] string width = "1in",
        [Description("Optional .NET format string for the detail cell, for example \"C2\".")] string? format_string = null,
        [Description("Optional expression for the totals row, for example \"=Sum(Fields!Amount.Value)\".")]
        string? footer_expression = null,
        [Description("Which table to change. Omit to use the first table in the report.")] string? tablix_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document => ColumnEditor.AddColumn(
            document, column_index, header_text, field_binding, width,
            format_string, footer_expression, tablix_name));

    /// <summary>Removes a column.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="column_index">Zero-based index of the column.</param>
    /// <param name="auto_adjust_page_width">Shrink the page to fit the remaining columns.</param>
    /// <param name="tablix_name">The table to change.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "remove_column", Destructive = true)]
    [Description(
        "Remove a column from an RDLC report table, including its cell in every row. By default the " +
        "page width is also shrunk to the remaining table width plus margins, which stops the report " +
        "printing a trailing blank page.")]
    public static ToolResponse RemoveColumn(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Zero-based column index, as reported by get_rdl_columns.")] int column_index,
        [Description("Shrink the page width to fit the remaining columns plus margins. True by default.")]
        bool auto_adjust_page_width = true,
        [Description("Which table to change. Omit to use the first table in the report.")] string? tablix_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ColumnEditor.RemoveColumn(document, column_index, auto_adjust_page_width, tablix_name));

    /// <summary>Reorders a column.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="from_index">Zero-based index of the column to move.</param>
    /// <param name="to_index">Zero-based index to move it to.</param>
    /// <param name="tablix_name">The table to change.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "move_column", Destructive = true)]
    [Description(
        "Move a column to a different position in an RDLC report table, carrying its width, header, " +
        "binding and styling with it. Indexes refer to the layout as it is now, before the move.")]
    public static ToolResponse MoveColumn(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Zero-based index of the column to move.")] int from_index,
        [Description("Zero-based index the column should end up at.")] int to_index,
        [Description("Which table to change. Omit to use the first table in the report.")] string? tablix_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ColumnEditor.MoveColumn(document, from_index, to_index, tablix_name));

    /// <summary>Styles a column.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="column_index">Zero-based index of the column.</param>
    /// <param name="apply_to">Which rows to style.</param>
    /// <param name="font_family">Font family name.</param>
    /// <param name="font_size">Font size with a unit suffix.</param>
    /// <param name="font_weight">Font weight.</param>
    /// <param name="font_style">Font style.</param>
    /// <param name="color">Text colour.</param>
    /// <param name="background_color">Cell background colour.</param>
    /// <param name="text_align">Horizontal alignment.</param>
    /// <param name="vertical_align">Vertical alignment.</param>
    /// <param name="tablix_name">The table to change.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "set_column_style", Destructive = true)]
    [Description(
        "Apply font, colour and alignment to a column in an RDLC report table. Every styling argument " +
        "is optional and only the ones supplied are changed, so this can be called repeatedly to build " +
        "up a style. Use apply_to to target the header row, the detail row, the totals row, or all three.")]
    public static ToolResponse SetColumnStyle(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Zero-based column index, as reported by get_rdl_columns.")] int column_index,
        [Description("Which rows to style: \"header\", \"data\", \"footer\" or \"all\". Defaults to \"all\".")]
        string apply_to = "all",
        [Description("Font family name, for example \"Segoe UI\".")] string? font_family = null,
        [Description("Font size with a unit suffix, for example \"10pt\".")] string? font_size = null,
        [Description("Font weight: \"Normal\", \"Bold\", \"Light\" or a numeric weight.")] string? font_weight = null,
        [Description("Font style: \"Normal\" or \"Italic\".")] string? font_style = null,
        [Description("Text colour, for example \"Black\" or \"#333333\".")] string? color = null,
        [Description("Cell background colour, for example \"White\" or \"#EEEEEE\".")] string? background_color = null,
        [Description("Horizontal alignment: \"Left\", \"Center\", \"Right\" or \"General\".")] string? text_align = null,
        [Description("Vertical alignment: \"Top\", \"Middle\" or \"Bottom\".")] string? vertical_align = null,
        [Description("Which table to change. Omit to use the first table in the report.")] string? tablix_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false)
    {
        if (!TryParseRowTarget(apply_to, out ColumnRowTarget target))
        {
            return ToolResponse.Failure(
                ToolErrorCodes.InvalidArgument,
                $"'{apply_to}' is not a valid value for apply_to.",
                "Use \"header\", \"data\", \"footer\" or \"all\".");
        }

        var style = new CellStyle
        {
            FontFamily = font_family,
            FontSize = font_size,
            FontWeight = font_weight,
            FontStyle = font_style,
            Color = color,
            BackgroundColor = background_color,
            TextAlign = text_align,
            VerticalAlign = vertical_align,
        };

        return ToolGuards.Mutate(filepath, dry_run, document =>
            ColumnEditor.SetColumnStyle(document, column_index, style, target, tablix_name));
    }

    private static bool TryParseRowTarget(string value, out ColumnRowTarget target) =>
        Enum.TryParse(value, ignoreCase: true, out target) && Enum.IsDefined(target);
}

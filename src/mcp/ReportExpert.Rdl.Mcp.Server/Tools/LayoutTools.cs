using System.ComponentModel;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Core.Editing;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Mcp.Server.Tools;

/// <summary>
/// Tools that change page geometry and individual textboxes.
/// </summary>
[McpServerToolType]
public static class LayoutTools
{
    /// <summary>Changes page geometry.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="page_width">New page width.</param>
    /// <param name="page_height">New page height.</param>
    /// <param name="orientation">Portrait or Landscape.</param>
    /// <param name="left_margin">New left margin.</param>
    /// <param name="right_margin">New right margin.</param>
    /// <param name="top_margin">New top margin.</param>
    /// <param name="bottom_margin">New bottom margin.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "set_page_setup", Destructive = true)]
    [Description(
        "Change the page size, orientation or margins of an RDLC report. Every argument is optional " +
        "and only the ones supplied are changed. Setting orientation swaps the page width and height " +
        "when they do not already match it, since RDL has no separate orientation setting.")]
    public static ToolResponse SetPageSetup(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("New page width with a unit suffix, for example \"8.5in\" or \"21cm\".")] string? page_width = null,
        [Description("New page height with a unit suffix, for example \"11in\" or \"29.7cm\".")] string? page_height = null,
        [Description("\"Portrait\" or \"Landscape\". Applied by swapping the page dimensions.")] string? orientation = null,
        [Description("New left margin with a unit suffix, for example \"0.5in\".")] string? left_margin = null,
        [Description("New right margin with a unit suffix.")] string? right_margin = null,
        [Description("New top margin with a unit suffix.")] string? top_margin = null,
        [Description("New bottom margin with a unit suffix.")] string? bottom_margin = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false)
    {
        var change = new PageSetupChange
        {
            PageWidth = page_width,
            PageHeight = page_height,
            Orientation = orientation,
            LeftMargin = left_margin,
            RightMargin = right_margin,
            TopMargin = top_margin,
            BottomMargin = bottom_margin,
        };

        return ToolGuards.Mutate(filepath, dry_run, document => PageEditor.SetPageSetup(document, change));
    }

    /// <summary>Sets the text a named textbox displays.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="textbox_name">The textbox's name.</param>
    /// <param name="value">The new text or expression.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "set_textbox_value", Destructive = true)]
    [Description(
        "Set the text or expression a named textbox displays in an RDLC report. Use this for titles, " +
        "labels and any cell outside a table, and whenever several cells share the same text so that " +
        "update_column_header cannot tell them apart. Find textbox names with get_rdl_report_items.")]
    public static ToolResponse SetTextboxValue(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("The textbox name, as reported by get_rdl_report_items or get_rdl_columns.")] string textbox_name,
        [Description("The new content. Start it with = to make it an expression; anything else is literal text.")]
        string value,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            TextboxEditor.SetTextboxValue(document, textbox_name, value));

    /// <summary>Shows or hides a named report item.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="item_name">The item's name.</param>
    /// <param name="hidden">true, false, or an RDL expression.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "set_report_item_visibility", Destructive = true)]
    [Description(
        "Show or hide a named report item (Image, Textbox, Tablix, Chart, Rectangle, Line, …) by " +
        "setting Visibility/Hidden. Pass hidden=true to hide, hidden=false to show, or an expression " +
        "such as \"=Not Parameters!ShowLogo.Value\". Prefer this over remove_report_item when the " +
        "item should stay in the definition. Find names with get_rdl_report_items.")]
    public static ToolResponse SetReportItemVisibility(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("The report item Name, as reported by get_rdl_report_items.")] string item_name,
        [Description("\"true\" to hide, \"false\" to show, or an RDL expression starting with =.")]
        string hidden,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ReportItemEditor.SetVisibility(document, item_name, hidden));

    /// <summary>Removes a named report item from the layout.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="item_name">The item's name.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "remove_report_item", Destructive = true)]
    [Description(
        "Remove a named report item (Image, Textbox, Rectangle, Line, Chart, free-standing Tablix, …) " +
        "from the layout. Refuses when the item is the only content of a tablix cell — hide it with " +
        "set_report_item_visibility instead. Find names with get_rdl_report_items.")]
    public static ToolResponse RemoveReportItem(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("The report item Name, as reported by get_rdl_report_items.")] string item_name,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ReportItemEditor.RemoveReportItem(document, item_name));
}

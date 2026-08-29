using System.ComponentModel;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Core.Reading;
using ReportExpert.Rdl.Core.Validation;

namespace ReportExpert.Rdl.Mcp.Server.Tools;

/// <summary>
/// Tools that inspect a report definition without changing it.
/// </summary>
/// <remarks>
/// The <c>[Description]</c> text on each method and parameter is what the language model reads
/// when choosing a tool, so it is written for that audience rather than for a developer.
/// </remarks>
[McpServerToolType]
public static class ReadTools
{
    /// <summary>Summarizes the structure of a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <returns>Dataset, parameter and column counts, plus a line per dataset.</returns>
    [McpServerTool(Name = "describe_rdl_report", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get a high-level summary of an RDLC report: how many datasets, parameters and table columns " +
        "it has, and what query each dataset runs. Start here when you do not yet know the shape of a report.")]
    public static ToolResponse DescribeRdlReport(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath) =>
        ToolGuards.Read(filepath, document => ReportReader.Describe(document, filepath));

    /// <summary>Reads the datasets of a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="field_limit">How many fields to include per dataset.</param>
    /// <param name="field_pattern">A regular expression that field names must match.</param>
    /// <returns>The datasets with their queries and, optionally, their fields.</returns>
    [McpServerTool(Name = "get_rdl_datasets", ReadOnly = true, Idempotent = true)]
    [Description(
        "List the datasets in an RDLC report with their data source, command type, command text and " +
        "query parameters. Field details are omitted by default because reports often declare hundreds " +
        "of fields; ask for them with field_limit when you need them.")]
    public static ToolResponse GetRdlDataSets(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Fields to return per dataset: 0 for counts only (default), -1 for all fields, or a positive number to cap the list.")]
        int field_limit = 0,
        [Description("Optional case-insensitive regular expression; only field names matching it are returned. Ignored when field_limit is 0.")]
        string? field_pattern = null) =>
        ToolGuards.Read(filepath, document => DataSetReader.GetDataSets(document, field_limit, field_pattern));

    /// <summary>Reads the parameters of a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <returns>The parameters with their types, prompts, defaults and allowed values.</returns>
    [McpServerTool(Name = "get_rdl_parameters", ReadOnly = true, Idempotent = true)]
    [Description(
        "List the report parameters of an RDLC report with their data type, prompt, default values and " +
        "allowed values.")]
    public static ToolResponse GetRdlParameters(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath) =>
        ToolGuards.Read(filepath, ParameterReader.GetParameters);

    /// <summary>Reads the columns of a tablix.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="tablix_name">The tablix to read.</param>
    /// <returns>The columns with their headers, widths, bindings and formats.</returns>
    [McpServerTool(Name = "get_rdl_columns", ReadOnly = true, Idempotent = true)]
    [Description(
        "List the columns of a table in an RDLC report, each with its zero-based index, header text, " +
        "width, field binding and format string. The index returned here is the one every column " +
        "editing tool expects.")]
    public static ToolResponse GetRdlColumns(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Which table to read. Omit to use the first table in the report, which is what most reports have.")]
        string? tablix_name = null) =>
        ToolGuards.Read(filepath, document => ColumnReader.GetColumns(document, tablix_name));

    /// <summary>Lists the tablixes in a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <returns>The tablixes with their names, bound datasets and grid sizes.</returns>
    [McpServerTool(Name = "get_rdl_tablixes", ReadOnly = true, Idempotent = true)]
    [Description(
        "List every table in an RDLC report with its name, the dataset it is bound to, and how many " +
        "rows and columns it has. Use this when a report has more than one table, to find the name to " +
        "pass as tablix_name elsewhere.")]
    public static ToolResponse GetRdlTablixes(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath) =>
        ToolGuards.Read(filepath, ReportReader.GetTablixes);

    /// <summary>Reads the page geometry of a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <returns>Page size, orientation, margins and the resulting usable width.</returns>
    [McpServerTool(Name = "get_rdl_page_setup", ReadOnly = true, Idempotent = true)]
    [Description(
        "Read the page size, orientation and margins of an RDLC report, along with the usable width " +
        "left between the margins. Check this before widening a table, since content wider than the " +
        "usable width spills onto extra pages.")]
    public static ToolResponse GetRdlPageSetup(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath) =>
        ToolGuards.Read(filepath, ReportReader.GetPageSetup);

    /// <summary>Inventories the report items in a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="kind">Restrict results to one item kind.</param>
    /// <returns>The report items with their names, positions and sizes.</returns>
    [McpServerTool(Name = "get_rdl_report_items", ReadOnly = true, Idempotent = true)]
    [Description(
        "List the items in an RDLC report layout: textboxes, images, rectangles, lines, subreports, " +
        "tables and charts. Each entry gives the item's name, position, size and, for textboxes, the " +
        "text it displays. Use this to find the name of a textbox to edit.")]
    public static ToolResponse GetRdlReportItems(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Restrict the results to one kind, for example \"Textbox\" or \"Image\". Omit to list every kind.")]
        string? kind = null) =>
        ToolGuards.Read(filepath, document => ReportReader.GetReportItems(document, kind));

    /// <summary>Finds where a dataset field is used.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="field_name">The field to search for.</param>
    /// <param name="dataset_name">Restrict the search to one dataset.</param>
    /// <returns>Every expression that references the field.</returns>
    [McpServerTool(Name = "find_field_usages", ReadOnly = true, Idempotent = true)]
    [Description(
        "Find every expression in an RDLC report that references a dataset field, with the textbox " +
        "each one lives in. Run this before renaming or removing a field to see what would break.")]
    public static ToolResponse FindFieldUsages(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("The field name, without the Fields! prefix, for example \"Amount\".")] string field_name,
        [Description("Only report references that resolve against this dataset. Omit to search every dataset.")]
        string? dataset_name = null) =>
        ToolGuards.Read(filepath, document => RdlValidator.FindFieldUsages(document, field_name, dataset_name));

    /// <summary>Validates a report definition.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <returns>The issues and warnings found.</returns>
    [McpServerTool(Name = "validate_rdl", ReadOnly = true, Idempotent = true)]
    [Description(
        "Check an RDLC report for problems that stop it rendering: expressions referencing fields their " +
        "dataset does not declare, tables bound to datasets that do not exist, and missing structure. " +
        "Run this after a series of edits to confirm the report is still sound.")]
    public static ToolResponse ValidateRdl(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath) =>
        ToolGuards.Read(filepath, RdlValidator.Validate);
}

using System.ComponentModel;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Core.Editing;

namespace ReportExpert.Rdl.Mcp.Server.Tools;

/// <summary>
/// Tools that change the datasets of a report.
/// </summary>
[McpServerToolType]
public static class DataSetTools
{
    /// <summary>Replaces a dataset's query command text.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="dataset_name">The dataset to change.</param>
    /// <param name="new_sproc">The new command text.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "update_stored_procedure", Destructive = true)]
    [Description(
        "Point a dataset in an RDLC report at a different stored procedure or query by replacing its " +
        "command text. The dataset's declared fields are left alone, so check that the new query still " +
        "returns them.")]
    public static ToolResponse UpdateStoredProcedure(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the dataset to change, as reported by get_rdl_datasets.")] string dataset_name,
        [Description("The new command text, usually a stored procedure name such as \"usp_GetOrders\".")] string new_sproc,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            DataSetEditor.UpdateStoredProcedure(document, dataset_name, new_sproc));

    /// <summary>Adds a field to a dataset.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="dataset_name">The dataset to add to.</param>
    /// <param name="field_name">The field name.</param>
    /// <param name="data_field">The underlying source column.</param>
    /// <param name="type_name">The CLR type name.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "add_dataset_field", Destructive = true)]
    [Description(
        "Declare a new field on a dataset in an RDLC report. Do this before binding a column to the " +
        "field, since an expression referencing an undeclared field fails validation.")]
    public static ToolResponse AddDataSetField(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the dataset to add the field to.")] string dataset_name,
        [Description("The field name used in expressions, for example \"Amount\" for =Fields!Amount.Value.")] string field_name,
        [Description("The column or property name in the underlying data source, often the same as field_name.")] string data_field,
        [Description("The CLR type name, for example \"System.String\", \"System.Decimal\" or \"System.DateTime\".")] string type_name,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            DataSetEditor.AddField(document, dataset_name, field_name, data_field, type_name));

    /// <summary>Removes a field from a dataset.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="dataset_name">The dataset to remove from.</param>
    /// <param name="field_name">The field to remove.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "remove_dataset_field", Destructive = true)]
    [Description(
        "Remove a field declaration from a dataset in an RDLC report. Expressions that still reference " +
        "the field are left as they are and will fail validation, so run find_field_usages first.")]
    public static ToolResponse RemoveDataSetField(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the dataset to remove the field from.")] string dataset_name,
        [Description("The field to remove.")] string field_name,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            DataSetEditor.RemoveField(document, dataset_name, field_name));

    /// <summary>Renames a dataset field.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="dataset_name">The dataset that owns the field.</param>
    /// <param name="field_name">The current field name.</param>
    /// <param name="new_field_name">The new field name.</param>
    /// <param name="update_references">Rewrite expressions that reference the field.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "rename_dataset_field", Destructive = true)]
    [Description(
        "Rename a field on a dataset in an RDLC report and, by default, rewrite every expression that " +
        "references it so the report stays valid. Only whole field names are rewritten, so renaming " +
        "\"Total\" will not disturb \"TotalTax\".")]
    public static ToolResponse RenameDataSetField(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the dataset that owns the field.")] string dataset_name,
        [Description("The current field name.")] string field_name,
        [Description("The new field name.")] string new_field_name,
        [Description("Rewrite Fields! references throughout the report. True by default.")] bool update_references = true,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            DataSetEditor.RenameField(document, dataset_name, field_name, new_field_name, update_references));

    /// <summary>Adds a dataset.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="dataset_name">The new dataset's name.</param>
    /// <param name="command_text">The query command text.</param>
    /// <param name="command_type">The query command type.</param>
    /// <param name="datasource_name">The data source to run against.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "add_dataset", Destructive = true)]
    [Description(
        "Add a dataset to an RDLC report. The dataset starts with no fields; declare them with " +
        "add_dataset_field before binding anything to it.")]
    public static ToolResponse AddDataSet(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name for the new dataset.")] string dataset_name,
        [Description("The query command text, for example a stored procedure name. Omit for a dataset supplied by the host application.")]
        string? command_text = null,
        [Description("The command type: \"StoredProcedure\", \"Text\" or \"TableDirect\".")] string? command_type = null,
        [Description("Name of the data source the query runs against, as declared in the report.")] string? datasource_name = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            DataSetEditor.AddDataSet(document, dataset_name, command_text, command_type, datasource_name));

    /// <summary>Removes a dataset.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="dataset_name">The dataset to remove.</param>
    /// <param name="force">Remove even when a table is bound to it.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "remove_dataset", Destructive = true)]
    [Description(
        "Remove a dataset from an RDLC report. Refused when a table is still bound to it, because that " +
        "leaves the report unable to render; rebind the table first, or pass force=true if you are sure.")]
    public static ToolResponse RemoveDataSet(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the dataset to remove.")] string dataset_name,
        [Description("Remove the dataset even when a table is bound to it. False by default.")] bool force = false,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            DataSetEditor.RemoveDataSet(document, dataset_name, force));
}

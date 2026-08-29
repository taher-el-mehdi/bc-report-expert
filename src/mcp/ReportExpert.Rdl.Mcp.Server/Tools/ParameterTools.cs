using System.ComponentModel;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Core.Editing;

namespace ReportExpert.Rdl.Mcp.Server.Tools;

/// <summary>
/// Tools that change the parameters of a report.
/// </summary>
[McpServerToolType]
public static class ParameterTools
{
    /// <summary>Adds a report parameter.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="data_type">The parameter's data type.</param>
    /// <param name="prompt">The prompt shown to the user.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "add_parameter", Destructive = true)]
    [Description(
        "Add a report parameter to an RDLC report. The parameter is created with no default value, so " +
        "the report will prompt for it; follow up with update_parameter to give it a default.")]
    public static ToolResponse AddParameter(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("The parameter name used in expressions, for example \"StartDate\" for =Parameters!StartDate.Value.")]
        string name,
        [Description("The data type: \"String\", \"Boolean\", \"DateTime\", \"Integer\" or \"Float\".")] string data_type,
        [Description("The prompt shown to the user, for example \"Start date\".")] string prompt,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ParameterEditor.AddParameter(document, name, data_type, prompt));

    /// <summary>Updates a report parameter.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="name">The parameter to update.</param>
    /// <param name="prompt">A new prompt.</param>
    /// <param name="default_value">A new default value.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "update_parameter", Destructive = true)]
    [Description(
        "Change the prompt or default value of an existing report parameter in an RDLC report. Supply " +
        "at least one of prompt and default_value; whichever you omit is left as it is.")]
    public static ToolResponse UpdateParameter(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the parameter to update.")] string name,
        [Description("The new prompt shown to the user. Omit to leave the prompt unchanged.")] string? prompt = null,
        [Description("The new default value, either a literal or an expression such as \"=Today()\". Omit to leave it unchanged.")]
        string? default_value = null,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document =>
            ParameterEditor.UpdateParameter(document, name, prompt, default_value));

    /// <summary>Removes a report parameter.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="name">The parameter to remove.</param>
    /// <param name="force">Remove even when the parameter is still referenced.</param>
    /// <param name="dry_run">Preview the change instead of writing it.</param>
    /// <returns>What changed, or a diff when previewing.</returns>
    [McpServerTool(Name = "remove_parameter", Destructive = true)]
    [Description(
        "Remove a report parameter from an RDLC report. Refused when expressions or dataset queries " +
        "still reference it, since that leaves the report unable to render; rewrite those first, or " +
        "pass force=true if you are sure.")]
    public static ToolResponse RemoveParameter(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("Name of the parameter to remove.")] string name,
        [Description("Remove the parameter even when it is still referenced. False by default.")] bool force = false,
        [Description("Preview only: return a unified diff of what would change and leave the file untouched.")]
        bool dry_run = false) =>
        ToolGuards.Mutate(filepath, dry_run, document => ParameterEditor.RemoveParameter(document, name, force));
}

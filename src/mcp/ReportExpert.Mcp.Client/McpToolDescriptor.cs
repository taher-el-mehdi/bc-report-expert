using System.Text.Json;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// A tool offered by a connected MCP server, described in the form a language model needs.
/// </summary>
/// <param name="ServerId">Which configured server offers the tool.</param>
/// <param name="ToolName">The tool's name as the server knows it.</param>
/// <param name="QualifiedName">
/// The name the model calls it by, namespaced so two servers can both offer a <c>search</c>.
/// </param>
/// <param name="Description">What the tool does, written for the model.</param>
/// <param name="JsonSchema">The JSON Schema for the tool's arguments.</param>
public sealed record McpToolDescriptor(
    string ServerId,
    string ToolName,
    string QualifiedName,
    string Description,
    JsonElement JsonSchema)
{
    /// <summary>
    /// Whether the tool only reads. A host can run these without asking the user first.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>, so a server that declares no hints is treated as
    /// capable of writing. Guessing the other way would let an unannotated tool edit a file
    /// silently.
    /// </remarks>
    public bool IsReadOnly { get; init; }

    /// <summary>Whether the tool makes changes that are not easily undone.</summary>
    public bool IsDestructive { get; init; }
}

/// <summary>
/// What a tool returned.
/// </summary>
/// <param name="IsError">Whether the server reported the call as failed.</param>
/// <param name="Text">
/// The textual content of the result, which is what gets appended to the conversation.
/// </param>
public sealed record McpToolResult(bool IsError, string Text);

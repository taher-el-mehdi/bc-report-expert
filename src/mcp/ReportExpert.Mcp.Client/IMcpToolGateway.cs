namespace ReportExpert.Mcp.Client;

/// <summary>
/// The application's view of every MCP tool available to it.
/// </summary>
/// <remarks>
/// This is the whole surface the agent loop depends on. Nothing above it knows how many servers
/// there are, that they are child processes, or that MCP is involved at all, which is what lets a
/// second server be a configuration entry rather than a code change.
/// </remarks>
public interface IMcpToolGateway
{
    /// <summary>
    /// Lists the tools every connected server offers, connecting on first use.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The available tools. Empty when no server could be reached.</returns>
    /// <remarks>
    /// A server that fails to start is logged and skipped rather than throwing, so one broken
    /// entry in the configuration cannot take the assistant offline.
    /// </remarks>
    ValueTask<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a tool by its qualified name.
    /// </summary>
    /// <param name="qualifiedName">The name from <see cref="McpToolDescriptor.QualifiedName"/>.</param>
    /// <param name="arguments">The arguments, matching the tool's schema.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The result. A failure is reported as a result with <see cref="McpToolResult.IsError"/> set
    /// rather than as an exception, because the model needs to read the failure and try again.
    /// </returns>
    ValueTask<McpToolResult> CallToolAsync(
        string qualifiedName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);
}

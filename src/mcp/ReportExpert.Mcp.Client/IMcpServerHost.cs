namespace ReportExpert.Mcp.Client;

/// <summary>
/// One tool reported by a server, for the settings list.
/// </summary>
/// <param name="Name">The tool's bare name.</param>
/// <param name="QualifiedName">The <c>server__tool</c> name used for enablement.</param>
/// <param name="Description">What the tool does.</param>
public sealed record McpToolStatus(string Name, string QualifiedName, string Description);

/// <summary>
/// The state of one configured MCP server, for display.
/// </summary>
/// <param name="Id">The server's identifier.</param>
/// <param name="Command">The executable it launches.</param>
/// <param name="IsConnected">Whether a session is currently established.</param>
/// <param name="ToolCount">How many tools it last reported.</param>
/// <param name="LastError">Why it last failed, or <see langword="null"/> when it never has.</param>
/// <param name="Tools">The individual tools, when the host enumerated them.</param>
public sealed record McpServerStatus(
    string Id,
    string Command,
    bool IsConnected,
    int ToolCount,
    string? LastError,
    IReadOnlyList<McpToolStatus> Tools)
{
    /// <summary>Creates a status with no tool details yet.</summary>
    public McpServerStatus(string id, string command, bool isConnected, int toolCount, string? lastError)
        : this(id, command, isConnected, toolCount, lastError, [])
    {
    }
}

/// <summary>
/// Owns the MCP server processes for the application's lifetime.
/// </summary>
/// <remarks>
/// Separate from <see cref="IMcpToolGateway"/> because the two have different audiences: the agent
/// loop wants tools, and the settings screen wants processes it can inspect and restart.
/// </remarks>
public interface IMcpServerHost
{
    /// <summary>Where the server list is configured.</summary>
    string ConfigurationPath { get; }

    /// <summary>
    /// Reports the current state of each configured server.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One entry per server.</returns>
    ValueTask<IReadOnlyList<McpServerStatus>> DescribeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts every server down and starts again from the configuration on disk.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes once the servers are back.</returns>
    ValueTask RestartAsync(CancellationToken cancellationToken = default);
}

using System.IO;
using ReportExpert.Mcp.Client;

namespace ReportExpert.App.Shell;

/// <summary>
/// Owns the MCP server processes and the gateway the assistant calls through.
/// </summary>
/// <remarks>
/// <para>
/// The server list comes from <c>%AppData%\ReportExpert\mcp-servers.json</c>. The shipped
/// <c>rdl</c> entry is stored as a path relative to the application directory
/// (<c>mcp\rdlc\rdlc-mcp.exe</c>) and resolved at launch, so Store, installer, and debug
/// builds all start the copy that sits beside the running exe.
/// </para>
/// <para>
/// Nothing here is referenced by the Copilot module: the assistant sees only
/// <see cref="IMcpToolGateway"/>, so adding a second server never reaches the agent loop.
/// </para>
/// </remarks>
public sealed class McpHost : IMcpServerHost, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpToolRegistry? _registry;
    private bool _disposed;

    /// <inheritdoc />
    public string ConfigurationPath => McpServerConfiguration.DefaultPath;

    /// <summary>
    /// The tools currently available, or <see langword="null"/> before <see cref="Start"/>.
    /// </summary>
    public IMcpToolGateway? Gateway => _registry;

    /// <summary>
    /// Reads the configuration, seeding it on first run, and prepares the servers.
    /// </summary>
    /// <remarks>
    /// No process is launched here. Connections are established the first time the assistant asks
    /// for a tool, so starting the application stays fast and a user who never opens the assistant
    /// never pays for a server.
    /// </remarks>
    public void Start()
    {
        EnsureConfiguration();
        _registry = new McpToolRegistry(McpServerConfiguration.Load());
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<McpServerStatus>> DescribeAsync(CancellationToken cancellationToken = default)
    {
        if (_registry is null)
            return [];

        var tools = await _registry.ListToolsAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. _registry.Connections.Select(connection =>
            {
                var serverTools = tools
                    .Where(tool => tool.ServerId == connection.Id)
                    .Select(tool => new McpToolStatus(tool.ToolName, tool.QualifiedName, tool.Description))
                    .ToList();

                return new McpServerStatus(
                    connection.Id,
                    Command(connection.Id),
                    connection.IsConnected,
                    serverTools.Count,
                    connection.LastError,
                    serverTools);
            }),
        ];
    }

    /// <inheritdoc />
    public async ValueTask RestartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-seed / repair the path before reconnecting, in case the config still points at a
            // leftover Debug build or an old flat copy of rdlc-mcp.exe.
            EnsureConfiguration();

            if (_registry is { } existing)
                await existing.DisposeAsync().ConfigureAwait(false);

            _registry = new McpToolRegistry(McpServerConfiguration.Load());
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Shuts every server down.</summary>
    /// <returns>A task that completes once the child processes have exited.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_registry is { } registry)
            await registry.DisposeAsync().ConfigureAwait(false);

        _gate.Dispose();
    }

    private static void EnsureConfiguration()
    {
        string? bundled = BundledMcpLocator.Find();

        try
        {
            if (!File.Exists(McpServerConfiguration.DefaultPath))
            {
                if (bundled is null)
                    return;

                McpServerConfiguration.Save(
                [
                    new McpServerDefinition
                    {
                        Id = BundledMcpLocator.ServerId,
                        Command = BundledMcpLocator.RelativeCommand,
                    },
                ]);
                return;
            }

            var servers = McpServerConfiguration.LoadAll();
            var repaired = BundledMcpLocator.Repair(servers, bundled);
            if (!ReferenceEquals(repaired, servers))
                McpServerConfiguration.Save(repaired);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only profile means no tools, not a failure to start.
        }
    }

    private string Command(string serverId) =>
        McpServerConfiguration.Load()
            .FirstOrDefault(server => string.Equals(server.Id, serverId, StringComparison.Ordinal))
            ?.Command
        ?? string.Empty;
}

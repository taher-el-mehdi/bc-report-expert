using System.IO;
using System.Reflection;
using ReportExpert.Mcp.Client;

namespace ReportExpert.App.Shell;

/// <summary>
/// Owns the MCP server processes and the gateway the assistant calls through.
/// </summary>
/// <remarks>
/// <para>
/// The server list comes from <c>%AppData%\ReportExpert\mcp-servers.json</c>. On first run that
/// file is written pointing at the RDLC server shipped alongside the application, so tools work
/// out of the box while still being reconfigurable by hand.
/// </para>
/// <para>
/// Nothing here is referenced by the Copilot module: the assistant sees only
/// <see cref="IMcpToolGateway"/>, so adding a second server never reaches the agent loop.
/// </para>
/// </remarks>
public sealed class McpHost : IMcpServerHost, IAsyncDisposable
{
    private const string BundledServerId = "rdl";
    private const string BundledServerExecutable = "rdlc-mcp.exe";
    private const string BundledServerRelativeDirectory = "mcp\\rdlc";

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
            // Re-seed / repair the path before reconnecting, in case the config still points at an
            // old flat copy of rdlc-mcp.exe that is missing its dependency DLLs.
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

    /// <summary>
    /// The RDLC server that ships with the application, or <see langword="null"/> when it is not
    /// beside the executable.
    /// </summary>
    private static string? BundledServerPath()
    {
        string? directory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty);
        if (string.IsNullOrEmpty(directory))
            return null;

        // Prefer the self-contained mcp/rdlc folder (exe + Hosting + Rdl.Core + …).
        string nested = Path.Combine(directory, BundledServerRelativeDirectory, BundledServerExecutable);
        if (IsRunnableServer(nested))
            return nested;

        // Legacy flat copy next to the app — only accept it when its dependencies are present.
        string flat = Path.Combine(directory, BundledServerExecutable);
        return IsRunnableServer(flat) ? flat : null;
    }

    /// <summary>
    /// True when <paramref name="exePath"/> exists and sits next to the assemblies the server
    /// needs to start. A lone exe without Microsoft.Extensions.Hosting.dll crashes immediately.
    /// </summary>
    private static bool IsRunnableServer(string exePath)
    {
        if (!File.Exists(exePath))
            return false;

        string? directory = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(directory))
            return false;

        return File.Exists(Path.Combine(directory, "Microsoft.Extensions.Hosting.dll"))
            && File.Exists(Path.Combine(directory, "ReportExpert.Rdl.Core.dll"));
    }

    private static void EnsureConfiguration()
    {
        string? bundled = BundledServerPath();

        try
        {
            if (!File.Exists(McpServerConfiguration.DefaultPath))
            {
                if (bundled is null)
                    return;

                McpServerConfiguration.Save(
                [
                    new McpServerDefinition { Id = BundledServerId, Command = bundled },
                ]);
                return;
            }

            if (bundled is null)
                return;

            // An earlier build wrote a path to a flat rdlc-mcp.exe that cannot start. Point the
            // bundled "rdl" entry at the complete mcp/rdlc folder instead.
            var servers = McpServerConfiguration.LoadAll().ToList();
            int index = servers.FindIndex(server =>
                string.Equals(server.Id, BundledServerId, StringComparison.Ordinal));

            if (index < 0)
                return;

            var current = servers[index];
            if (IsRunnableServer(current.Expanded().Command))
                return;

            servers[index] = current with { Command = bundled };
            McpServerConfiguration.Save(servers);
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

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// Presents the tools of every configured MCP server as one catalogue.
/// </summary>
/// <remarks>
/// Names are namespaced per server, so two servers offering a <c>search</c> tool do not collide
/// and a call can be routed without a separate lookup table.
/// </remarks>
public sealed class McpToolRegistry : IMcpToolGateway, IAsyncDisposable
{
    /// <summary>
    /// What separates the server id from the tool name in a qualified name.
    /// </summary>
    /// <remarks>
    /// A double underscore rather than the more natural dot: these names are sent to the chat
    /// completions API as function names, and that API rejects anything outside
    /// <c>[A-Za-z0-9_-]</c>.
    /// </remarks>
    public const string NameSeparator = "__";

    private readonly IReadOnlyList<StdioMcpServerConnection> _connections;
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>Creates a registry over a set of servers.</summary>
    /// <param name="servers">The servers to aggregate.</param>
    /// <param name="loggerFactory">Where connections report their lifecycle and stderr.</param>
    public McpToolRegistry(IReadOnlyList<McpServerDefinition> servers, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(servers);

        var factory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = factory.CreateLogger<McpToolRegistry>();

        _connections =
        [
            .. servers.Select(server => new StdioMcpServerConnection(
                server,
                factory.CreateLogger($"Mcp.{server.Id}"))),
        ];
    }

    /// <summary>The configured servers and whether each is currently connected.</summary>
    public IReadOnlyList<StdioMcpServerConnection> Connections => _connections;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var descriptors = new List<McpToolDescriptor>();

        foreach (var connection in _connections)
        {
            IList<McpClientTool> tools;

            try
            {
                tools = await connection.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One server failing to start must not take the others, or the assistant, down.
                Log.ServerUnavailable(_logger, exception, connection.Id);
                continue;
            }

            foreach (var tool in tools)
                descriptors.Add(Describe(connection.Id, tool));
        }

        return descriptors;
    }

    /// <inheritdoc />
    public async ValueTask<McpToolResult> CallToolAsync(
        string qualifiedName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedName);

        if (!TrySplit(qualifiedName, out string serverId, out string toolName))
            return Unroutable(qualifiedName, $"'{qualifiedName}' is not a qualified tool name.");

        var connection = _connections.FirstOrDefault(c => string.Equals(c.Id, serverId, StringComparison.Ordinal));
        if (connection is null)
            return Unroutable(qualifiedName, $"No MCP server named '{serverId}' is configured.");

        try
        {
            var result = await connection.CallToolAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);

            return new McpToolResult(result.IsError ?? false, TextOf(result));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Returned rather than thrown: the model is mid-conversation and can recover from a
            // described failure, but an exception would abandon the whole turn.
            Log.ToolCallFailed(_logger, exception, toolName, serverId);

            return new McpToolResult(
                IsError: true,
                Text: $"The tool '{qualifiedName}' could not be run: {exception.Message}");
        }
    }

    /// <summary>Shuts every server down.</summary>
    /// <returns>A task that completes once all child processes have exited.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var connection in _connections)
            await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the name a model calls a tool by.
    /// </summary>
    /// <param name="serverId">The server's identifier.</param>
    /// <param name="toolName">The tool's own name.</param>
    /// <returns>The qualified name.</returns>
    public static string Qualify(string serverId, string toolName) => $"{serverId}{NameSeparator}{toolName}";

    /// <summary>
    /// Splits a qualified name back into its server and tool parts.
    /// </summary>
    /// <param name="qualifiedName">The qualified name.</param>
    /// <param name="serverId">The server identifier.</param>
    /// <param name="toolName">The tool name.</param>
    /// <returns><see langword="true"/> when the name was well formed.</returns>
    public static bool TrySplit(string qualifiedName, out string serverId, out string toolName)
    {
        serverId = string.Empty;
        toolName = string.Empty;

        if (string.IsNullOrEmpty(qualifiedName))
            return false;

        // The first separator wins, because a tool name may itself contain a double underscore
        // but a server id may not.
        int index = qualifiedName.IndexOf(NameSeparator, StringComparison.Ordinal);
        if (index <= 0 || index + NameSeparator.Length >= qualifiedName.Length)
            return false;

        serverId = qualifiedName[..index];
        toolName = qualifiedName[(index + NameSeparator.Length)..];

        return true;
    }

    private static McpToolDescriptor Describe(string serverId, McpClientTool tool)
    {
        var annotations = tool.ProtocolTool.Annotations;

        return new McpToolDescriptor(
            serverId,
            tool.Name,
            Qualify(serverId, tool.Name),
            tool.Description ?? string.Empty,
            tool.JsonSchema)
        {
            IsReadOnly = annotations?.ReadOnlyHint ?? false,
            IsDestructive = annotations?.DestructiveHint ?? false,
        };
    }

    private McpToolResult Unroutable(string qualifiedName, string reason)
    {
        Log.UnroutableCall(_logger, qualifiedName, reason);

        return new McpToolResult(IsError: true, Text: reason);
    }

    private static string TextOf(CallToolResult result)
    {
        if (result.Content.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();

        foreach (var block in result.Content)
        {
            if (block is not TextContentBlock text)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append(text.Text);
        }

        // Non-text content, such as an image, has no useful textual form; saying so beats an
        // empty result the model cannot interpret.
        return builder.Length > 0
            ? builder.ToString()
            : $"The tool returned {result.Content.Count} non-text content block(s).";
    }
}

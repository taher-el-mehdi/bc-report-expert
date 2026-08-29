using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// A live connection to one MCP server running as a child process.
/// </summary>
/// <remarks>
/// <para>
/// The server is started on first use rather than at construction, so configuring a server that
/// is never used costs nothing.
/// </para>
/// <para>
/// A stdio server is a child process, and child processes die. Every call therefore checks whether
/// the session is still alive and restarts it if not; the caller sees a slow call rather than a
/// permanently broken assistant.
/// </para>
/// </remarks>
public sealed class StdioMcpServerConnection : IAsyncDisposable
{
    private readonly McpServerDefinition _definition;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private McpClient? _client;
    private bool _disposed;

    /// <summary>Creates a connection.</summary>
    /// <param name="definition">Which server to launch.</param>
    /// <param name="logger">Where to report lifecycle events and the server's stderr.</param>
    public StdioMcpServerConnection(McpServerDefinition definition, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _definition = definition;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>The identifier this server's tools are namespaced under.</summary>
    public string Id => _definition.Id;

    /// <summary>Whether a session is currently established.</summary>
    public bool IsConnected => _client is not null && !IsFaulted(_client);

    /// <summary>
    /// The last connection failure, or <see langword="null"/> when the server has never failed.
    /// Surfaced in the settings UI so a misconfigured server is visible.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Lists the tools this server offers.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tools as the protocol describes them.</returns>
    public async ValueTask<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var client = await ConnectAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            Log.DroppedListingTools(_logger, exception, Id);

            client = await ReconnectAsync(cancellationToken).ConfigureAwait(false);
            return await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invokes a tool on this server.
    /// </summary>
    /// <param name="toolName">The tool's name as the server knows it, without any prefix.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The protocol result.</returns>
    public async ValueTask<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        var client = await ConnectAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            Log.DroppedCallingTool(_logger, exception, Id, toolName);

            // Retried because the tools are individually safe to repeat: reads are pure, and a
            // write that was interrupted by the process dying did not commit, since the server
            // writes atomically.
            client = await ReconnectAsync(cancellationToken).ConfigureAwait(false);

            return await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Shuts the child process down.</summary>
    /// <returns>A task that completes once the process has exited.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await CloseAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private async ValueTask<McpClient> ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is { } existing && !IsFaulted(existing))
            return existing;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client is { } current && !IsFaulted(current))
                return current;

            return await StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<McpClient> ReconnectAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseAsync().ConfigureAwait(false);
            return await StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Starts the child process. The caller must hold <see cref="_gate"/>.</summary>
    private async ValueTask<McpClient> StartAsync(CancellationToken cancellationToken)
    {
        var options = new StdioClientTransportOptions
        {
            Name = _definition.Id,
            Command = _definition.Command,
            Arguments = [.. _definition.Arguments],
            WorkingDirectory = _definition.WorkingDirectory,
            // The server logs to stderr because stdout carries the protocol, so this is the only
            // way to see why one is misbehaving.
            StandardErrorLines = line => Log.ServerOutput(_logger, _definition.Id, line),
        };

        if (_definition.Environment.Count > 0)
        {
            options.EnvironmentVariables ??= new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach ((string key, string value) in _definition.Environment)
                options.EnvironmentVariables[key] = value;
        }

        try
        {
            _client = await McpClient.CreateAsync(
                new StdioClientTransport(options),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            LastError = null;
            Log.Connected(_logger, _definition.Id, _definition.Command);

            return _client;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LastError = exception.Message;
            Log.StartFailed(_logger, exception, _definition.Id, _definition.Command);
            throw;
        }
    }

    /// <summary>Tears the session down. The caller must hold <see cref="_gate"/>, or be disposing.</summary>
    private async ValueTask CloseAsync()
    {
        if (_client is null)
            return;

        var client = _client;
        _client = null;

        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Shutting down a process that has already died is expected and not worth surfacing.
            Log.UncleanShutdown(_logger, exception, _definition.Id);
        }
    }

    /// <summary>
    /// Whether the session has ended. <see cref="McpClient.Completion"/> completes when the
    /// transport closes, which for a stdio server means the process exited.
    /// </summary>
    private static bool IsFaulted(McpClient client) => client.Completion.IsCompleted;

    private static bool IsTransportFailure(Exception exception) =>
        exception is ClientTransportClosedException or IOException or ObjectDisposedException
        || exception is InvalidOperationException { InnerException: IOException };
}

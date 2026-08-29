using Microsoft.Extensions.Logging;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// Source-generated log messages for the MCP client.
/// </summary>
/// <remarks>
/// A server's stderr is forwarded line by line, so logging here sits on a hot path. Generated
/// delegates keep that allocation-free and skip formatting entirely when the level is disabled.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Connected to MCP server '{ServerId}' ({Command}).")]
    public static partial void Connected(ILogger logger, string serverId, string command);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Could not start MCP server '{ServerId}' ({Command}).")]
    public static partial void StartFailed(ILogger logger, Exception exception, string serverId, string command);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "MCP server '{ServerId}' dropped while listing tools; reconnecting.")]
    public static partial void DroppedListingTools(ILogger logger, Exception exception, string serverId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "MCP server '{ServerId}' dropped during '{Tool}'; reconnecting.")]
    public static partial void DroppedCallingTool(ILogger logger, Exception exception, string serverId, string tool);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "MCP server '{ServerId}' did not shut down cleanly.")]
    public static partial void UncleanShutdown(ILogger logger, Exception exception, string serverId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Debug,
        Message = "[{ServerId}] {Line}")]
    public static partial void ServerOutput(ILogger logger, string serverId, string line);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "MCP server '{ServerId}' is unavailable; its tools are being skipped.")]
    public static partial void ServerUnavailable(ILogger logger, Exception exception, string serverId);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Error,
        Message = "Calling '{Tool}' on MCP server '{ServerId}' failed.")]
    public static partial void ToolCallFailed(ILogger logger, Exception exception, string tool, string serverId);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "Could not route a call to '{Tool}': {Reason}")]
    public static partial void UnroutableCall(ILogger logger, string tool, string reason);
}

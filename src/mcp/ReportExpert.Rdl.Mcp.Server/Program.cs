using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ReportExpert.Rdl.Mcp.Server;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the JSON-RPC stream, so anything written there that is not a protocol message
// corrupts the session. Every log line goes to stderr instead, where the client can surface it.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "reportexpert-rdlc",
            Version = ThisAssembly.Version,
        };

        options.ServerInstructions =
            "Tools for inspecting and editing Microsoft RDLC report definitions. " +
            "Every tool returns {ok, data, error}: check `ok` first, read `data` on success, and read " +
            "`error.hint` on failure for the next step. Start with describe_rdl_report to learn the " +
            "shape of a report. Every tool that writes accepts dry_run=true, which returns a unified " +
            "diff without changing the file, and takes an automatic backup when it does write.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellationToken) =>
    {
        try
        {
            return await next(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reaching here means the failure happened outside a tool body, which in practice means
            // the SDK could not bind the supplied arguments. Without this the client would see an
            // opaque protocol error instead of something it can act on.
            return ProtocolFailure(context.Params?.Name, ex);
        }
    }));

await builder.Build().RunAsync().ConfigureAwait(false);

static CallToolResult ProtocolFailure(string? toolName, Exception exception)
{
    var response = ToolResponse.Failure(
        ToolErrorCodes.InvalidArgument,
        $"The arguments supplied to '{toolName ?? "the tool"}' could not be read: {exception.Message}",
        "Check the tool's input schema and resend the call with correctly named and typed arguments. Nothing was changed.");

    return new CallToolResult
    {
        IsError = true,
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(response) }],
    };
}

/// <summary>Build-time facts about this assembly.</summary>
internal static class ThisAssembly
{
    /// <summary>The informational version reported to MCP clients.</summary>
    public static string Version { get; } =
        typeof(ToolResponse).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}

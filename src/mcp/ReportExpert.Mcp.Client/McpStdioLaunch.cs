using ModelContextProtocol.Client;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// Builds <see cref="StdioClientTransportOptions"/> that start on every Windows machine,
/// including paths that contain spaces.
/// </summary>
/// <remarks>
/// The MCP C# SDK wraps any non-<c>cmd.exe</c> command as <c>cmd.exe /c {path}</c>. Setting
/// <c>Command</c> to <c>cmd.exe</c> ourselves skips that wrap. The executable path is then
/// passed as its own ArgumentList entry so the runtime quotes it; cmd's "exactly two quotes
/// around an executable" rule keeps <c>Report Expert</c> and <c>Program Files</c> intact.
/// </remarks>
public static class McpStdioLaunch
{
    /// <summary>
    /// Fills <paramref name="options"/> from <paramref name="definition"/> so the transport
    /// can start the process.
    /// </summary>
    public static void Apply(StdioClientTransportOptions options, McpServerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(definition);

        string command = BundledMcpLocator.ResolveCommand(definition.Command);
        IReadOnlyList<string> arguments = definition.Arguments;

        if (OperatingSystem.IsWindows())
        {
            options.Command = "cmd.exe";
            options.Arguments = arguments.Count == 0
                ? ["/c", command]
                : ["/c", command, .. arguments];
        }
        else
        {
            options.Command = command;
            options.Arguments = [.. arguments];
        }

        if (!string.IsNullOrWhiteSpace(definition.WorkingDirectory))
        {
            options.WorkingDirectory = BundledMcpLocator.ResolveCommand(definition.WorkingDirectory);
        }
        else if (Path.IsPathRooted(command))
        {
            string? directory = Path.GetDirectoryName(command);
            if (!string.IsNullOrEmpty(directory))
                options.WorkingDirectory = directory;
        }
    }
}

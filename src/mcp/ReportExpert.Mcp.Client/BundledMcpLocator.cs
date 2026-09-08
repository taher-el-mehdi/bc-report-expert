namespace ReportExpert.Mcp.Client;

/// <summary>
/// Finds the RDLC MCP server that ships beside the application.
/// </summary>
/// <remarks>
/// The path must be resolved from the running executable, never from a machine-specific
/// absolute path saved during an earlier debug session. Store, installer, and portable
/// layouts all place <c>mcp/rdlc/rdlc-mcp.exe</c> next to the app.
/// </remarks>
public static class BundledMcpLocator
{
    /// <summary>Well-known id of the server Report Expert ships.</summary>
    public const string ServerId = "rdl";

    /// <summary>File name of the shipped Windows executable.</summary>
    public const string ExecutableFileName = "rdlc-mcp.exe";

    /// <summary>
    /// Portable command written to <c>mcp-servers.json</c>. Resolved against the application
    /// directory at launch so the same file works on every machine.
    /// </summary>
    public static string RelativeCommand { get; } = Path.Combine("mcp", "rdlc", ExecutableFileName);

    /// <summary>
    /// Absolute path to a runnable bundled server beside <paramref name="applicationDirectory"/>,
    /// or <see langword="null"/> when it is not present.
    /// </summary>
    public static string? Find(string? applicationDirectory = null)
    {
        string? directory = string.IsNullOrWhiteSpace(applicationDirectory)
            ? AppContext.BaseDirectory
            : applicationDirectory;

        if (string.IsNullOrWhiteSpace(directory))
            return null;

        directory = Path.GetFullPath(directory);

        string nested = Path.Combine(directory, "mcp", "rdlc", ExecutableFileName);
        if (IsRunnable(nested))
            return nested;

        string flat = Path.Combine(directory, ExecutableFileName);
        return IsRunnable(flat) ? flat : null;
    }

    /// <summary>
    /// True when <paramref name="exePath"/> exists and sits next to the assemblies the server
    /// needs to start. A lone exe without its dependency DLLs crashes immediately.
    /// </summary>
    public static bool IsRunnable(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return false;

        string? directory = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(directory))
            return false;

        return File.Exists(Path.Combine(directory, "Microsoft.Extensions.Hosting.dll"))
            && File.Exists(Path.Combine(directory, "ReportExpert.Rdl.Core.dll"));
    }

    /// <summary>
    /// Expands environment variables and turns an app-relative command into an absolute path.
    /// Bare names such as <c>npx</c> stay unchanged so they can be resolved from PATH.
    /// </summary>
    public static string ResolveCommand(string command, string? applicationDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        string expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (Path.IsPathRooted(expanded))
            return Path.GetFullPath(expanded);

        if (!expanded.Contains(Path.DirectorySeparatorChar) && !expanded.Contains(Path.AltDirectorySeparatorChar))
            return expanded;

        string root = string.IsNullOrWhiteSpace(applicationDirectory)
            ? AppContext.BaseDirectory
            : applicationDirectory;

        return Path.GetFullPath(Path.Combine(root, expanded));
    }

    /// <summary>
    /// Points the shipped <c>rdl</c> entry at the portable relative command, replacing leftover
    /// Visual Studio or other-machine absolute paths. Other servers are left untouched.
    /// </summary>
    public static IReadOnlyList<McpServerDefinition> Repair(
        IReadOnlyList<McpServerDefinition> servers,
        string? bundledPath)
    {
        ArgumentNullException.ThrowIfNull(servers);

        var list = servers.ToList();
        int index = list.FindIndex(server =>
            string.Equals(server.Id, ServerId, StringComparison.Ordinal));

        if (index < 0)
        {
            if (bundledPath is null)
                return servers;

            list.Add(new McpServerDefinition { Id = ServerId, Command = RelativeCommand });
            return list;
        }

        if (ShouldRetarget(list[index]))
        {
            list[index] = list[index] with { Command = RelativeCommand };
            return list;
        }

        return servers;
    }

    /// <summary>
    /// The shipped server is always retargeted: a leftover Debug path under another clone must
    /// not keep winning just because that folder still exists on the developer's machine.
    /// </summary>
    private static bool ShouldRetarget(McpServerDefinition server)
    {
        string command = server.Command.Trim();
        if (string.IsNullOrEmpty(command))
            return true;

        if (string.Equals(NormalizeRelative(command), NormalizeRelative(RelativeCommand), StringComparison.OrdinalIgnoreCase))
            return false;

        string fileName = Path.GetFileName(Environment.ExpandEnvironmentVariables(command));
        return string.Equals(fileName, ExecutableFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, Path.GetFileNameWithoutExtension(ExecutableFileName), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelative(string command) =>
        command.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim();
}

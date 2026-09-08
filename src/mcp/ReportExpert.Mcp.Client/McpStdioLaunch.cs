using System.Diagnostics;
using System.Text;
using ModelContextProtocol.Client;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// Starts MCP server processes in a way that works from a Store package and from folders
/// whose names contain spaces.
/// </summary>
/// <remarks>
/// <para>
/// The MCP C# SDK wraps every Windows command in <c>cmd.exe /c</c>. <c>cmd.exe</c> lives
/// outside the MSIX package, so the child <em>breaks away</em> and loses package identity.
/// Loading <c>hostfxr.dll</c> from <c>WindowsApps</c> then fails with access denied
/// (HRESULT 0x80070005). Real <c>.exe</c> files are therefore started with
/// <see cref="ProcessStartInfo.FileName"/> set to the executable — spaces are fine, and
/// the child keeps the parent's package identity.
/// </para>
/// <para>
/// Bare names such as <c>npx</c> still go through <c>cmd.exe</c>, because those are
/// usually <c>.cmd</c> shims that need a shell.
/// </para>
/// </remarks>
public static class McpStdioLaunch
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// True when <paramref name="command"/> is an existing Windows executable that should be
    /// started directly instead of through <c>cmd.exe</c>.
    /// </summary>
    public static bool IsDirectExecutable(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        string resolved = BundledMcpLocator.ResolveCommand(command);
        return resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(resolved);
    }

    /// <summary>
    /// Starts <paramref name="definition"/> as a child process with redirected stdio.
    /// Callers own the process and must dispose it.
    /// </summary>
    public static Process StartDirect(McpServerDefinition definition, Action<string> onStandardError)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(onStandardError);

        string command = BundledMcpLocator.ResolveCommand(definition.Command);
        string? workingDirectory = !string.IsNullOrWhiteSpace(definition.WorkingDirectory)
            ? BundledMcpLocator.ResolveCommand(definition.WorkingDirectory)
            : Path.GetDirectoryName(command);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
        };

        foreach (string argument in definition.Arguments)
            startInfo.ArgumentList.Add(argument);

        foreach ((string key, string value) in definition.Environment)
            startInfo.Environment[key] = value;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
                onStandardError(args.Data);
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new IOException($"Failed to start MCP server process '{command}'.");
        }

        process.BeginErrorReadLine();
        return process;
    }

    /// <summary>
    /// Fills <paramref name="options"/> for the SDK transport. Used only for commands that
    /// are not a real <c>.exe</c> (for example <c>npx</c>).
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

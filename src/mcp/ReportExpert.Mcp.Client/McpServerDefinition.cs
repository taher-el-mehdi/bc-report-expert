using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReportExpert.Mcp.Client;

/// <summary>
/// One MCP server the application should connect to.
/// </summary>
public sealed record McpServerDefinition
{
    /// <summary>
    /// Short identifier, used to namespace this server's tool names. Keep it to letters, digits
    /// and underscores, since it becomes part of a name the model calls.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The executable to launch.</summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>Arguments passed to the executable.</summary>
    [JsonPropertyName("args")]
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Working directory for the child process. Defaults to the application's.</summary>
    [JsonPropertyName("cwd")]
    public string? WorkingDirectory { get; init; }

    /// <summary>Environment variables to set on the child process.</summary>
    [JsonPropertyName("env")]
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether to connect to this server. Lets one be parked without deleting it.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Expands environment variables and resolves app-relative commands such as
    /// <c>mcp\rdlc\rdlc-mcp.exe</c> against the running application directory.
    /// </summary>
    /// <returns>A definition with its paths resolved.</returns>
    public McpServerDefinition Expanded() => this with
    {
        Command = BundledMcpLocator.ResolveCommand(Command),
        WorkingDirectory = WorkingDirectory is null
            ? null
            : BundledMcpLocator.ResolveCommand(WorkingDirectory),
        Arguments = [.. Arguments.Select(System.Environment.ExpandEnvironmentVariables)],
    };
}

/// <summary>
/// Reading and writing the list of MCP servers.
/// </summary>
/// <remarks>
/// The file lives beside the other user settings so it can be edited by hand, which is how MCP
/// servers are configured everywhere else and is what people will expect.
/// </remarks>
public static class McpServerConfiguration
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Where the configuration lives by default.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "ReportExpert",
        "mcp-servers.json");

    /// <summary>
    /// Reads the enabled server definitions.
    /// </summary>
    /// <param name="path">The configuration file, defaulting to <see cref="DefaultPath"/>.</param>
    /// <returns>The enabled servers, or an empty list when the file is absent or unreadable.</returns>
    /// <remarks>
    /// A malformed file yields an empty list rather than an exception. Configuration this
    /// peripheral should never stop the application from starting.
    /// </remarks>
    public static IReadOnlyList<McpServerDefinition> Load(string? path = null)
    {
        string resolved = path ?? DefaultPath;

        if (!File.Exists(resolved))
            return [];

        try
        {
            var file = JsonSerializer.Deserialize<McpServerFile>(File.ReadAllText(resolved), Options);

            return file?.Servers is null
                ? []
                : [.. file.Servers.Where(server => server.Enabled).Select(server => server.Expanded())];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Reads every server definition as stored on disk, including disabled ones, without expanding
    /// environment variables. Used when rewriting the file so placeholders like
    /// <c>%LOCALAPPDATA%</c> are preserved.
    /// </summary>
    /// <param name="path">The configuration file, defaulting to <see cref="DefaultPath"/>.</param>
    /// <returns>All servers, or an empty list when the file is absent or unreadable.</returns>
    public static IReadOnlyList<McpServerDefinition> LoadAll(string? path = null)
    {
        string resolved = path ?? DefaultPath;

        if (!File.Exists(resolved))
            return [];

        try
        {
            var file = JsonSerializer.Deserialize<McpServerFile>(File.ReadAllText(resolved), Options);
            return file?.Servers ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Writes the server definitions, creating the containing directory if needed.
    /// </summary>
    /// <param name="servers">The servers to record.</param>
    /// <param name="path">The configuration file, defaulting to <see cref="DefaultPath"/>.</param>
    public static void Save(IReadOnlyList<McpServerDefinition> servers, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(servers);

        string resolved = path ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);

        File.WriteAllText(resolved, JsonSerializer.Serialize(new McpServerFile { Servers = servers }, Options));
    }

    private sealed record McpServerFile
    {
        [JsonPropertyName("servers")]
        public IReadOnlyList<McpServerDefinition>? Servers { get; init; }
    }
}

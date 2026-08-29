using System.Globalization;

namespace ReportExpert.Rdl.Mcp.Server;

/// <summary>
/// Host configuration, read once from environment variables at startup.
/// </summary>
/// <remarks>
/// A stdio MCP server has no command line to speak of once a client has launched it, so
/// environment variables are the only practical configuration channel.
/// </remarks>
public static class ServerOptions
{
    /// <summary>
    /// Semicolon-separated directories that report definitions must live under.
    /// </summary>
    public const string AllowedRootsVariable = "RDLC_MCP_ALLOWED_ROOTS";

    /// <summary>Set to <c>0</c> or <c>false</c> to stop taking a backup before each edit.</summary>
    public const string BackupsEnabledVariable = "RDLC_MCP_BACKUPS";

    /// <summary>How many backups to keep per report before the oldest are deleted.</summary>
    public const string BackupRetentionVariable = "RDLC_MCP_BACKUP_RETENTION";

    private const int DefaultBackupRetention = 20;

    /// <summary>
    /// Directories that report definitions must live under, or empty when any path is allowed.
    /// </summary>
    public static IReadOnlyList<string> AllowedRoots { get; } = ReadAllowedRoots();

    /// <summary>Whether a timestamped backup is taken before each edit.</summary>
    public static bool BackupsEnabled { get; } = ReadBoolean(BackupsEnabledVariable, defaultValue: true);

    /// <summary>How many backups to keep per report.</summary>
    public static int BackupRetention { get; } =
        ReadInt32(BackupRetentionVariable, DefaultBackupRetention, minimum: 1, maximum: 1000);

    private static IReadOnlyList<string> ReadAllowedRoots()
    {
        string? raw = Environment.GetEnvironmentVariable(AllowedRootsVariable);
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return [.. raw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar))];
    }

    private static bool ReadBoolean(string variable, bool defaultValue)
    {
        string? raw = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        return raw.Trim().ToLowerInvariant() switch
        {
            "0" or "false" or "no" or "off" => false,
            "1" or "true" or "yes" or "on" => true,
            _ => defaultValue,
        };
    }

    private static int ReadInt32(string variable, int defaultValue, int minimum, int maximum)
    {
        string? raw = Environment.GetEnvironmentVariable(variable);

        if (string.IsNullOrWhiteSpace(raw) ||
            !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}

using System.Globalization;
using ReportExpert.Rdl.Core;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Mcp.Server;

/// <summary>
/// Timestamped copies of a report definition, taken before each edit.
/// </summary>
/// <remarks>
/// <para>
/// Backups live in a <c>.rdlc-backups</c> folder beside the report. Keeping them adjacent rather
/// than in a temp directory means the user can find and restore them without this server, which
/// matters when an agent has made a change they did not want.
/// </para>
/// <para>
/// Only the most recent <see cref="ServerOptions.BackupRetention"/> backups of any one report are
/// kept.
/// </para>
/// </remarks>
public static class BackupStore
{
    private const string FolderName = ".rdlc-backups";
    private const string TimestampFormat = "yyyyMMdd-HHmmss-fff";
    private const string Extension = ".bak";

    /// <summary>
    /// Copies a report definition into the backup folder.
    /// </summary>
    /// <param name="filePath">The report to back up.</param>
    /// <returns>Absolute path to the backup.</returns>
    public static string Create(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string folder = FolderFor(filePath);
        Directory.CreateDirectory(folder);

        string stamp = DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string backupPath = Path.Combine(folder, $"{Path.GetFileName(filePath)}.{stamp}{Extension}");

        File.Copy(filePath, backupPath, overwrite: true);
        Prune(folder, Path.GetFileName(filePath));

        return backupPath;
    }

    /// <summary>
    /// Lists the backups of a report, newest first.
    /// </summary>
    /// <param name="filePath">The report whose backups to list.</param>
    /// <returns>The backups.</returns>
    public static BackupsResult List(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var backups = Enumerate(FolderFor(filePath), Path.GetFileName(filePath))
            .Select(file => new BackupInfo(
                file.Name,
                file.FullName,
                file.LastWriteTimeUtc,
                file.Length))
            .ToList();

        return new BackupsResult(backups.Count, backups);
    }

    /// <summary>
    /// Restores a report from one of its backups.
    /// </summary>
    /// <param name="filePath">The report to overwrite.</param>
    /// <param name="backupFileName">
    /// The backup to restore, by file name. When <see langword="null"/> the most recent backup is
    /// used.
    /// </param>
    /// <returns>Absolute path to the backup that was restored.</returns>
    /// <exception cref="RdlNotFoundException">There is no such backup.</exception>
    /// <remarks>
    /// The current content is itself backed up first, so a restore can be undone.
    /// </remarks>
    public static string Restore(string filePath, string? backupFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string folder = FolderFor(filePath);
        var candidates = Enumerate(folder, Path.GetFileName(filePath)).ToList();

        if (candidates.Count == 0)
        {
            throw new RdlNotFoundException(
                RdlTarget.Backup,
                $"No backups exist for '{Path.GetFileName(filePath)}'.");
        }

        FileInfo chosen;
        if (string.IsNullOrWhiteSpace(backupFileName))
        {
            chosen = candidates[0];
        }
        else
        {
            // Match on the file name only: accepting a caller-supplied path here would let a
            // relative segment reach outside the backup folder.
            string wanted = Path.GetFileName(backupFileName);

            chosen = candidates.Find(c => string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw new RdlNotFoundException(
                    RdlTarget.Backup,
                    $"No backup named '{wanted}' exists. Available: {string.Join(", ", candidates.Take(5).Select(c => c.Name))}.");
        }

        if (File.Exists(filePath))
            Create(filePath);

        File.Copy(chosen.FullName, filePath, overwrite: true);
        return chosen.FullName;
    }

    /// <summary>The folder holding a report's backups, which may not exist yet.</summary>
    /// <param name="filePath">The report path.</param>
    /// <returns>The backup folder path.</returns>
    public static string FolderFor(string filePath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        return directory is null
            ? throw new ArgumentException($"'{filePath}' has no containing directory.", nameof(filePath))
            : Path.Combine(directory, FolderName);
    }

    private static IEnumerable<FileInfo> Enumerate(string folder, string reportFileName)
    {
        if (!Directory.Exists(folder))
            return [];

        return new DirectoryInfo(folder)
            .EnumerateFiles($"{reportFileName}.*{Extension}")
            .OrderByDescending(file => file.Name, StringComparer.Ordinal);
    }

    private static void Prune(string folder, string reportFileName)
    {
        var stale = Enumerate(folder, reportFileName).Skip(ServerOptions.BackupRetention);

        foreach (var file in stale)
        {
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
                // Failing to prune must never fail the edit that triggered the backup.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }
    }
}

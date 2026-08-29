using System.ComponentModel;
using ModelContextProtocol.Server;
using ReportExpert.Rdl.Core.Models;

namespace ReportExpert.Rdl.Mcp.Server.Tools;

/// <summary>
/// Tools for the automatic backups taken before every edit.
/// </summary>
[McpServerToolType]
public static class BackupTools
{
    /// <summary>Lists the backups of a report.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <returns>The backups, newest first.</returns>
    [McpServerTool(Name = "list_rdl_backups", ReadOnly = true, Idempotent = true)]
    [Description(
        "List the automatic backups of an RDLC report, newest first. One is taken before every edit " +
        "and kept in a .rdlc-backups folder beside the report.")]
    public static ToolResponse ListRdlBackups(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath) =>
        ToolGuards.Run(() => BackupStore.List(ToolGuards.ResolvePath(filepath, mustExist: false)));

    /// <summary>Restores a report from a backup.</summary>
    /// <param name="filepath">Path to the report definition.</param>
    /// <param name="backup_file_name">Which backup to restore.</param>
    /// <returns>What was restored.</returns>
    [McpServerTool(Name = "restore_rdl_backup", Destructive = true)]
    [Description(
        "Restore an RDLC report from one of its automatic backups, undoing recent edits. The current " +
        "content is itself backed up first, so a restore can be reversed. Omit backup_file_name to " +
        "roll back to the most recent backup.")]
    public static ToolResponse RestoreRdlBackup(
        [Description("Absolute or relative path to the .rdl or .rdlc file.")] string filepath,
        [Description("File name of the backup to restore, as reported by list_rdl_backups. Omit to use the newest.")]
        string? backup_file_name = null) =>
        ToolGuards.Replace(filepath, resolved =>
        {
            string restored = BackupStore.Restore(resolved, backup_file_name);

            return new EditOutcome($"Restored '{Path.GetFileName(resolved)}' from backup '{Path.GetFileName(restored)}'")
            {
                Details = new Dictionary<string, object?> { ["restored_from"] = restored },
            };
        });
}

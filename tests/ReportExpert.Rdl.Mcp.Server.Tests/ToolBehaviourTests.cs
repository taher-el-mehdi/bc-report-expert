using System.Text.Json;
using ReportExpert.Rdl.Mcp.Server;
using ReportExpert.Rdl.Mcp.Server.Tools;

namespace ReportExpert.Rdl.Mcp.Server.Tests;

/// <summary>
/// Dry runs and the automatic backups, which together are what make agent edits recoverable.
/// </summary>
public class SafetyTests
{
    [Fact]
    public void DryRun_LeavesTheFileUntouched()
    {
        using var context = new ToolTestContext();
        string before = context.ReadReport();

        var payload = ToolAssert.Ok(
            ColumnTools.UpdateColumnHeader(context.ReportPath, "Name", "Full Name", dry_run: true));

        Assert.True(payload.GetProperty("dry_run").GetBoolean());
        Assert.Equal(before, context.ReadReport());
    }

    [Fact]
    public void DryRun_ReturnsAUsefulDiff()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(
            ColumnTools.UpdateColumnHeader(context.ReportPath, "Name", "Full Name", dry_run: true));

        string diff = ToolAssert.String(payload, "diff");
        Assert.Contains("-", diff, StringComparison.Ordinal);
        Assert.Contains("<Value>Name</Value>", diff, StringComparison.Ordinal);
        Assert.Contains("<Value>Full Name</Value>", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_SaysSoInTheMessage()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(
            ColumnTools.RemoveColumn(context.ReportPath, 0, dry_run: true));

        Assert.StartsWith("[dry run", ToolAssert.String(payload, "message"), StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_TakesNoBackup()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(
            ColumnTools.UpdateColumnWidth(context.ReportPath, 0, "3in", dry_run: true));

        Assert.False(payload.TryGetProperty("backup_path", out _));
        Assert.False(Directory.Exists(Path.Combine(context.Directory, ".rdlc-backups")));
    }

    [Fact]
    public void RealWrite_TakesABackupFirst()
    {
        using var context = new ToolTestContext();
        string original = context.ReadReport();

        var payload = ToolAssert.Ok(ColumnTools.UpdateColumnWidth(context.ReportPath, 0, "3in"));

        string backupPath = ToolAssert.String(payload, "backup_path");
        Assert.True(File.Exists(backupPath));
        Assert.Equal(original, File.ReadAllText(backupPath));
        Assert.NotEqual(original, context.ReadReport());
    }

    [Fact]
    public void ListBackups_ReportsWhatWasTaken()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(ColumnTools.UpdateColumnWidth(context.ReportPath, 0, "3in"));
        ToolAssert.Ok(ColumnTools.UpdateColumnWidth(context.ReportPath, 1, "3in"));

        var payload = ToolAssert.Ok(BackupTools.ListRdlBackups(context.ReportPath));

        Assert.Equal(2, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ListBackups_IsEmptyRatherThanFailingWhenNoneExist()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(BackupTools.ListRdlBackups(context.ReportPath));

        Assert.Equal(0, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public void RestoreBackup_UndoesTheLastEdit()
    {
        using var context = new ToolTestContext();
        string original = context.ReadReport();

        ToolAssert.Ok(ColumnTools.UpdateColumnHeader(context.ReportPath, "Name", "Full Name"));
        Assert.NotEqual(original, context.ReadReport());

        ToolAssert.Ok(BackupTools.RestoreRdlBackup(context.ReportPath));

        Assert.Equal(original, context.ReadReport());
    }

    [Fact]
    public void RestoreBackup_IsItselfReversible()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(ColumnTools.UpdateColumnHeader(context.ReportPath, "Name", "Full Name"));
        string edited = context.ReadReport();

        ToolAssert.Ok(BackupTools.RestoreRdlBackup(context.ReportPath));

        // Restoring backed up the edited content, so the newest backup is the edit itself.
        ToolAssert.Ok(BackupTools.RestoreRdlBackup(context.ReportPath));
        Assert.Equal(edited, context.ReadReport());
    }

    [Fact]
    public void RestoreBackup_ReportsAMissingBackupClearly()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            BackupTools.RestoreRdlBackup(context.ReportPath),
            ToolErrorCodes.NotFound);

        Assert.Contains("list_rdl_backups", error.Hint, StringComparison.Ordinal);
    }
}

/// <summary>
/// End-to-end behaviour of the tools against a real file.
/// </summary>
public class ToolRoundTripTests
{
    [Fact]
    public void AddColumn_ThenReadItBack()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(DataSetTools.AddDataSetField(
            context.ReportPath, "MainDataset", "Status", "Status", "System.String"));

        ToolAssert.Ok(ColumnTools.AddColumn(
            context.ReportPath, -1, "Status", "=Fields!Status.Value", "1.25in"));

        var columns = ToolAssert.Ok(ReadTools.GetRdlColumns(context.ReportPath)).GetProperty("columns");

        Assert.Equal(4, columns.GetArrayLength());
        var added = columns[3];
        Assert.Equal("Status", added.GetProperty("header").GetString());
        Assert.Equal("1.25in", added.GetProperty("width").GetString());
        Assert.Equal("Status", added.GetProperty("field_name").GetString());
    }

    [Fact]
    public void RemoveColumn_AdjustsThePageWidth()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(ColumnTools.RemoveColumn(context.ReportPath, 1));

        var setup = ToolAssert.Ok(ReadTools.GetRdlPageSetup(context.ReportPath));
        Assert.Equal("3.50in", setup.GetProperty("page_width").GetString());
    }

    [Fact]
    public void MoveColumn_ReordersTheHeaders()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(ColumnTools.MoveColumn(context.ReportPath, 2, 0));

        var columns = ToolAssert.Ok(ReadTools.GetRdlColumns(context.ReportPath)).GetProperty("columns");
        Assert.Equal("Amount", columns[0].GetProperty("header").GetString());
    }

    [Fact]
    public void RenameField_KeepsTheReportValid()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(DataSetTools.RenameDataSetField(context.ReportPath, "MainDataset", "Amount", "Total"));

        var validation = ToolAssert.Ok(ReadTools.ValidateRdl(context.ReportPath));
        Assert.True(validation.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public void SetPageSetup_SwapsToLandscape()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(LayoutTools.SetPageSetup(context.ReportPath, orientation: "Landscape"));

        var setup = ToolAssert.Ok(ReadTools.GetRdlPageSetup(context.ReportPath));
        Assert.Equal("Landscape", setup.GetProperty("orientation").GetString());
    }

    [Fact]
    public void SetTextboxValue_ChangesOneCellOnly()
    {
        using var context = new ToolTestContext();

        ToolAssert.Ok(LayoutTools.SetTextboxValue(context.ReportPath, "HeaderAmount", "Total"));

        var columns = ToolAssert.Ok(ReadTools.GetRdlColumns(context.ReportPath)).GetProperty("columns");
        Assert.Equal("Total", columns[2].GetProperty("header").GetString());
        Assert.Equal("ID", columns[0].GetProperty("header").GetString());
    }

    [Fact]
    public void SetColumnStyle_RejectsAnUnknownTarget()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            ColumnTools.SetColumnStyle(context.ReportPath, 0, apply_to: "sideways", font_weight: "Bold"),
            ToolErrorCodes.InvalidArgument);

        Assert.Contains("header", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void FindFieldUsages_LocatesADetailBinding()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(ReadTools.FindFieldUsages(context.ReportPath, "Amount"));

        Assert.Equal(1, payload.GetProperty("count").GetInt32());
        Assert.Equal("DataAmount", payload.GetProperty("usages")[0].GetProperty("location").GetString());
    }

    [Fact]
    public void GetDataSets_RespectsTheFieldLimit()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(ReadTools.GetRdlDataSets(context.ReportPath, field_limit: 2));
        var main = payload.GetProperty("datasets")[0];

        Assert.Equal(2, main.GetProperty("fields").GetArrayLength());
        Assert.True(main.GetProperty("fields_truncated").GetBoolean());
        Assert.Equal(4, main.GetProperty("field_count").GetInt32());
    }

    [Fact]
    public void GetReportItems_FiltersByKind()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(ReadTools.GetRdlReportItems(context.ReportPath, "Textbox"));

        Assert.Equal(6, payload.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ConcurrentEditsToTheSameReportDoNotCorruptIt()
    {
        using var context = new ToolTestContext();

        // Ten tools writing at once must serialize rather than interleave; a torn write would
        // leave the file unparseable.
        Parallel.For(0, 10, index =>
            ColumnTools.UpdateColumnWidth(context.ReportPath, index % 3, $"{1 + (index % 3)}in"));

        var validation = ToolAssert.Ok(ReadTools.ValidateRdl(context.ReportPath));
        Assert.True(validation.GetProperty("valid").GetBoolean());
    }
}

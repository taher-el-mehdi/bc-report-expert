using System.Text.Json;
using ReportExpert.Rdl.Mcp.Server;
using ReportExpert.Rdl.Mcp.Server.Tools;

namespace ReportExpert.Rdl.Mcp.Server.Tests;

/// <summary>
/// The envelope shape every tool shares.
/// </summary>
public class ToolEnvelopeTests
{
    [Fact]
    public void SuccessfulRead_CarriesDataAndNoError()
    {
        using var context = new ToolTestContext();

        var response = ReadTools.DescribeRdlReport(context.ReportPath);

        var envelope = ToolAssert.Serialize(response);
        Assert.True(envelope.GetProperty("ok").GetBoolean());
        Assert.True(envelope.TryGetProperty("data", out _));
        Assert.False(envelope.TryGetProperty("error", out _));
    }

    [Fact]
    public void FailedRead_CarriesErrorAndNoData()
    {
        var response = ReadTools.DescribeRdlReport(
            Path.Combine(Path.GetTempPath(), "definitely-not-here-7a1c.rdlc"));

        var envelope = ToolAssert.Serialize(response);
        Assert.False(envelope.GetProperty("ok").GetBoolean());
        Assert.False(envelope.TryGetProperty("data", out _));

        var error = envelope.GetProperty("error");
        Assert.Equal(ToolErrorCodes.FileNotFound, error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("hint").GetString()));
    }

    [Fact]
    public void ReadPayload_UsesThePropertyNames()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(ReadTools.DescribeRdlReport(context.ReportPath));

        Assert.True(payload.TryGetProperty("report_summary", out var summary));
        Assert.True(payload.TryGetProperty("filepath", out _));
        Assert.Equal(2, summary.GetProperty("datasets").GetInt32());
        Assert.Equal(2, summary.GetProperty("parameters").GetInt32());
        Assert.Equal(3, summary.GetProperty("table_columns").GetInt32());
    }

    [Fact]
    public void WritePayload_KeepsSuccessAndMessageForCompatibility()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(
            ColumnTools.UpdateColumnHeader(context.ReportPath, "Name", "Full Name"));

        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Contains("Full Name", ToolAssert.String(payload, "message"), StringComparison.Ordinal);
    }

    [Fact]
    public void OmittedOptionalFieldsAreAbsentRatherThanNull()
    {
        using var context = new ToolTestContext();

        var payload = ToolAssert.Ok(ReadTools.GetRdlParameters(context.ReportPath));
        var parameter = payload.GetProperty("parameters")[0];

        Assert.False(parameter.TryGetProperty("default_values", out _));
        Assert.False(parameter.TryGetProperty("valid_values", out _));
    }
}

/// <summary>
/// How each kind of failure is classified.
/// </summary>
public class ToolErrorMappingTests
{
    [Fact]
    public void MissingFile_IsFileNotFound()
    {
        var response = ReadTools.ValidateRdl(Path.Combine(Path.GetTempPath(), "absent-1f4b.rdlc"));

        ToolAssert.Failed(response, ToolErrorCodes.FileNotFound);
    }

    [Fact]
    public void WrongExtension_IsInvalidPath()
    {
        var error = ToolAssert.Failed(
            ReadTools.ValidateRdl("C:\\reports\\notes.txt"),
            ToolErrorCodes.InvalidPath);

        Assert.Contains(".rdl", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyPath_IsInvalidPath()
    {
        ToolAssert.Failed(ReadTools.ValidateRdl("   "), ToolErrorCodes.InvalidPath);
    }

    [Fact]
    public void MalformedXml_IsInvalidRdl()
    {
        using var context = new ToolTestContext("not valid xml <>");

        ToolAssert.Failed(ReadTools.DescribeRdlReport(context.ReportPath), ToolErrorCodes.InvalidRdl);
    }

    [Fact]
    public void UnknownDataset_IsNotFound_WithADiscoveryHint()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            DataSetTools.UpdateStoredProcedure(context.ReportPath, "Ghost", "usp_X"),
            ToolErrorCodes.NotFound);

        Assert.Contains("get_rdl_datasets", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownParameter_IsNotFound_WithADiscoveryHint()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            ParameterTools.UpdateParameter(context.ReportPath, "Ghost", prompt: "x"),
            ToolErrorCodes.NotFound);

        Assert.Contains("get_rdl_parameters", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownTablix_IsNotFound_WithADiscoveryHint()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            ReadTools.GetRdlColumns(context.ReportPath, "Ghost"),
            ToolErrorCodes.NotFound);

        Assert.Contains("get_rdl_tablixes", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void OutOfRangeColumnIndex_IsInvalidArgument()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            ColumnTools.UpdateColumnWidth(context.ReportPath, 99, "1in"),
            ToolErrorCodes.InvalidArgument);

        Assert.Contains("column_index", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateField_IsRefused_WithTheDomainHint()
    {
        using var context = new ToolTestContext();

        var error = ToolAssert.Failed(
            DataSetTools.AddDataSetField(context.ReportPath, "MainDataset", "Amount", "Amount", "System.Decimal"),
            ToolErrorCodes.Refused);

        Assert.Contains("different field name", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovingABoundDataset_IsRefused()
    {
        using var context = new ToolTestContext();

        ToolAssert.Failed(
            DataSetTools.RemoveDataSet(context.ReportPath, "MainDataset"),
            ToolErrorCodes.Refused);
    }

    [Fact]
    public void EditThatWouldBreakTheReport_IsRolledBack()
    {
        using var context = new ToolTestContext();
        string before = context.ReadReport();

        // Amount is bound in the detail row, so removing its declaration breaks the report.
        var error = ToolAssert.Failed(
            DataSetTools.RemoveDataSetField(context.ReportPath, "MainDataset", "Amount"),
            ToolErrorCodes.ValidationFailed);

        Assert.Contains("unchanged", error.Hint, StringComparison.Ordinal);
        Assert.Equal(before, context.ReadReport());
    }

    [Fact]
    public void AnAlreadyBrokenReportIsNotBlockedFromBeingEdited()
    {
        // A report that fails validation before the edit must stay editable, or an agent could
        // never repair one.
        string broken = SampleReport.Xml.Replace(
            "=Fields!Amount.Value",
            "=Fields!Ghost.Value",
            StringComparison.Ordinal);

        using var context = new ToolTestContext(broken);

        ToolAssert.Ok(ColumnTools.UpdateColumnWidth(context.ReportPath, 0, "3in"));
    }
}

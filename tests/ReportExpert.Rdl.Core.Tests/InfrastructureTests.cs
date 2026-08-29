using ReportExpert.Rdl.Core;
using ReportExpert.Rdl.Core.Editing;
using ReportExpert.Rdl.Core.Reading;

namespace ReportExpert.Rdl.Core.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public void MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(
            () => RdlDocument.Load(Path.Combine(Path.GetTempPath(), "no-such-report-9f2c.rdl")));
    }

    [Fact]
    public void MalformedXml_ThrowsInvalidRdl()
    {
        using var report = new TempReport("not valid xml <>");

        var exception = Assert.Throws<InvalidRdlException>(() => RdlDocument.Load(report.Path));
        Assert.Contains("well-formed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongRootElement_ThrowsInvalidRdl()
    {
        using var report = new TempReport("<NotAReport><Thing /></NotAReport>");

        var exception = Assert.Throws<InvalidRdlException>(() => RdlDocument.Load(report.Path));
        Assert.Contains("NotAReport", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentTypeDefinition_IsRefused()
    {
        // A report definition has no legitimate reason to declare a DTD, and allowing one opens
        // the door to entity expansion attacks against a server that reads agent-supplied paths.
        using var report = new TempReport("""
            <?xml version="1.0"?>
            <!DOCTYPE Report [<!ENTITY x "y">]>
            <Report />
            """);

        Assert.Throws<InvalidRdlException>(() => RdlDocument.Load(report.Path));
    }
}

/// <summary>Round-trip behaviour of the XML reader and writer.</summary>
public class RdlXmlIOTests
{
    [Fact]
    public void SaveAtomic_ThenLoad_PreservesContent()
    {
        using var report = new TempReport();

        var document = report.Load();
        ColumnEditor.UpdateColumnHeader(document, "Name", "Full Name");
        document.Save();

        Assert.Equal("Full Name", ColumnReader.GetColumns(report.Load()).Columns[1].Header);
    }

    [Fact]
    public void SaveAtomic_KeepsTheDeclaredNamespacePrefixes()
    {
        using var report = new TempReport();

        var document = report.Load();
        DataSetEditor.AddField(document, "MainDataset", "Extra", "Extra", "System.String");
        document.Save();

        string text = report.ReadText();
        Assert.Contains("xmlns:rd=", text, StringComparison.Ordinal);
        Assert.Contains("<rd:TypeName>System.String</rd:TypeName>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAtomic_WritesADoubleQuotedDeclarationWithoutABom()
    {
        using var report = new TempReport();

        var document = report.Load();
        document.Save();

        byte[] bytes = File.ReadAllBytes(report.Path);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "The report should not be written with a byte order mark.");
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", report.ReadText(), StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAtomic_LeavesNoStagingFilesBehind()
    {
        using var report = new TempReport();

        var document = report.Load();
        document.Save();

        Assert.Single(Directory.GetFiles(report.Directory));
    }

    [Fact]
    public void Load_ResolvesTheNamespaceFromTheRoot()
    {
        // A report in the 2008 schema must round-trip in the 2008 schema.
        const string legacyNamespace = "http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition";
        string xml = SampleReport.Xml.Replace(SampleReport.Namespace, legacyNamespace, StringComparison.Ordinal);

        using var report = new TempReport(xml);

        var document = report.Load();
        Assert.Equal(legacyNamespace, document.Ns.NamespaceName);

        ColumnEditor.UpdateColumnWidth(document, 0, "2in");
        document.Save();

        Assert.Contains(legacyNamespace, report.ReadText(), StringComparison.Ordinal);
        Assert.Equal("2in", ColumnReader.GetColumns(report.Load()).Columns[0].Width);
    }
}

/// <summary>Size parsing and formatting.</summary>
public class DimensionTests
{
    [Theory]
    [InlineData("1in", 1d)]
    [InlineData("2.5in", 2.5d)]
    [InlineData("2.54cm", 1d)]
    [InlineData("25.4mm", 1d)]
    [InlineData("72pt", 1d)]
    [InlineData("6pc", 1d)]
    [InlineData("3", 3d)]
    public void ToInches_ParsesEveryUnit(string value, double expected)
    {
        Assert.Equal(expected, Dimension.ToInches(value), precision: 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wide")]
    [InlineData("in")]
    public void ToInches_ReturnsZeroForUnparseableInput(string? value)
    {
        Assert.Equal(0d, Dimension.ToInches(value));
        Assert.False(Dimension.TryToInches(value, out _));
    }

    [Fact]
    public void ToInches_IsCultureInvariant()
    {
        // A decimal comma locale must not turn 2.5in into 25in.
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal(2.5d, Dimension.ToInches("2.5in"), precision: 6);
            Assert.Equal("2.50in", Dimension.FromInches(2.5d));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void FromInches_UsesTwoDecimalPlaces()
    {
        Assert.Equal("4.50in", Dimension.FromInches(4.5d));
        Assert.Equal("0.00in", Dimension.FromInches(0d));
    }

    [Fact]
    public void Validate_RejectsMalformedInput()
    {
        var exception = Assert.Throws<ArgumentException>(() => Dimension.Validate("wide", "width"));
        Assert.Equal("width", exception.ParamName);
    }
}

/// <summary>Row role inference, which every column tool depends on.</summary>
public class RowKindTests
{
    [Fact]
    public void HeaderAndDataRowsAreDistinguished()
    {
        var document = SampleReport.Parse();
        var tablix = TablixNavigator.RequireTablix(document);
        var rows = TablixNavigator.Rows(tablix, document.Ns);

        Assert.Equal(
            Core.Models.TablixRowKind.Header,
            TablixNavigator.DetectRowKind(TablixNavigator.Cells(rows[0], document.Ns), document.Ns));

        Assert.Equal(
            Core.Models.TablixRowKind.Data,
            TablixNavigator.DetectRowKind(TablixNavigator.Cells(rows[1], document.Ns), document.Ns));
    }

    [Fact]
    public void AggregateRowIsTreatedAsAFooter()
    {
        string xml = SampleReport.Xml
            .Replace("=Fields!ID.Value", "=Count(Fields!ID.Value)", StringComparison.Ordinal)
            .Replace("=Fields!Name.Value", "=Count(Fields!Name.Value)", StringComparison.Ordinal)
            .Replace("=Fields!Amount.Value", "=Sum(Fields!Amount.Value)", StringComparison.Ordinal);

        var document = RdlDocument.Parse(xml);
        var tablix = TablixNavigator.RequireTablix(document);

        Assert.NotNull(TablixNavigator.FindRow(tablix, document.Ns, Core.Models.TablixRowKind.Footer));
    }

    [Fact]
    public void SingleAggregateAmongBindingsStaysADataRow()
    {
        // One total in an otherwise ordinary detail row must not reclassify the whole row,
        // or the detail bindings would be treated as footer content.
        string xml = SampleReport.Xml.Replace(
            "=Fields!Amount.Value",
            "=Sum(Fields!Amount.Value)",
            StringComparison.Ordinal);

        var document = RdlDocument.Parse(xml);
        var tablix = TablixNavigator.RequireTablix(document);

        Assert.NotNull(TablixNavigator.FindRow(tablix, document.Ns, Core.Models.TablixRowKind.Data));
        Assert.Null(TablixNavigator.FindRow(tablix, document.Ns, Core.Models.TablixRowKind.Footer));
    }
}

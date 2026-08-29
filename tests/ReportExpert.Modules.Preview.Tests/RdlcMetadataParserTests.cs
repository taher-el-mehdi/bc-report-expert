using System.IO;
using ReportExpert.Modules.Preview.Services;

namespace ReportExpert.Modules.Preview.Tests;

public class RdlcMetadataParserTests
{
    [Fact]
    public void Parse_MinimalReport_ReturnsDatasetFieldsAndPageSize()
    {
        string path = WriteTempRdlc("""
            <?xml version="1.0" encoding="utf-8"?>
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition">
              <DataSets>
                <DataSet Name="Header">
                  <Fields>
                    <Field Name="No">
                      <DataField>No</DataField>
                    </Field>
                    <Field Name="Amount">
                      <DataField>Amount</DataField>
                    </Field>
                  </Fields>
                </DataSet>
              </DataSets>
              <ReportParameters>
                <ReportParameter Name="ShowLogo">
                  <DataType>Boolean</DataType>
                  <Nullable>true</Nullable>
                  <AllowBlank>false</AllowBlank>
                </ReportParameter>
              </ReportParameters>
              <Page>
                <PageWidth>11in</PageWidth>
                <PageHeight>8.5in</PageHeight>
              </Page>
            </Report>
            """);

        try
        {
            var metadata = new RdlcMetadataParser().Parse(path);

            Assert.Equal("Header", Assert.Single(metadata.DataSets).Name);
            Assert.Contains(metadata.DataSets[0].Fields, f => f.Name == "No");
            Assert.Equal("ShowLogo", Assert.Single(metadata.Parameters).Name);
            Assert.Equal(11, metadata.PageWidth);
            Assert.Equal(8.5, metadata.PageHeight);
            Assert.Equal("Landscape", metadata.Orientation);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_MissingFile_Throws()
    {
        var parser = new RdlcMetadataParser();
        string missing = Path.Combine(Path.GetTempPath(), $"re-missing-{Guid.NewGuid():N}.rdlc");
        Assert.ThrowsAny<Exception>(() => parser.Parse(missing));
    }

    [Fact]
    public void InferTypeFromName_AmountIsDecimal_ShowTotalIsBoolean()
    {
        Assert.Equal(typeof(decimal), RdlcMetadataParser.InferTypeFromName("Amount", "Amount"));
        Assert.Equal(typeof(bool), RdlcMetadataParser.InferTypeFromName("ShowTotal", "ShowTotal"));
        Assert.Equal(typeof(DateTime), RdlcMetadataParser.InferTypeFromName("OrderDate", "OrderDate"));
        Assert.Equal(typeof(int), RdlcMetadataParser.InferTypeFromName("LineNo", "Line_No"));
        Assert.Equal(typeof(string), RdlcMetadataParser.InferTypeFromName("CustomerName", "CustomerName"));
    }

    [Theory]
    [InlineData("ShowGroup", "ShowGroup", true)]
    [InlineData("HasLines", "HasLines", true)]
    [InlineData("AsmHeaderExists", "AsmHeaderExists", true)]
    [InlineData("CustomerName", "CustomerName", false)]
    [InlineData("Amount", "Amount", false)]
    public void IsBooleanFlagName_RecognizesBcFlags(string name, string dataField, bool expected)
    {
        Assert.Equal(expected, RdlcMetadataParser.IsBooleanFlagName(name, dataField));
    }

    private static string WriteTempRdlc(string xml)
    {
        string path = Path.Combine(Path.GetTempPath(), $"re-preview-parser-{Guid.NewGuid():N}.rdlc");
        File.WriteAllText(path, xml);
        return path;
    }
}

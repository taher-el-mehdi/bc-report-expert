using ReportExpert.Reporting.Parser;

namespace ReportExpert.Reporting.Parser.Tests;

public class RdlcMetadataParserTests
{
    [Fact]
    public void Parse_MinimalReport_ReturnsDatasetAndFields()
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
                  </Fields>
                </DataSet>
              </DataSets>
              <Page>
                <PageWidth>8.5in</PageWidth>
                <PageHeight>11in</PageHeight>
              </Page>
            </Report>
            """);

        try
        {
            var parser = new RdlcMetadataParser();
            var metadata = parser.Parse(path);

            Assert.Single(metadata.DataSets);
            Assert.Equal("Header", metadata.DataSets[0].Name);
            Assert.Contains(metadata.DataSets[0].Fields, f => f.Name == "No");
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
    public void Parse_EmptyDatasets_ReturnsNoDatasets()
    {
        string path = WriteTempRdlc("""
            <?xml version="1.0" encoding="utf-8"?>
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition">
              <DataSets />
              <Page>
                <PageWidth>210mm</PageWidth>
                <PageHeight>297mm</PageHeight>
              </Page>
            </Report>
            """);

        try
        {
            var metadata = new RdlcMetadataParser().Parse(path);
            Assert.Empty(metadata.DataSets);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempRdlc(string xml)
    {
        string path = Path.Combine(Path.GetTempPath(), $"re-parser-{Guid.NewGuid():N}.rdlc");
        File.WriteAllText(path, xml);
        return path;
    }
}

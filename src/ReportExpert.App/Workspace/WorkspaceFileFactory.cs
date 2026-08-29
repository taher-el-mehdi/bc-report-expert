using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ReportExpert.App.Workspace;

/// <summary>Creates blank AL reports and layout stubs inside an open project.</summary>
internal static class WorkspaceFileFactory
{
    public static string CreateReport(string fullPath, string objectName)
    {
        string safeName = SanitizeIdentifier(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var sb = new StringBuilder();
        sb.AppendLine($"report 50100 \"{safeName}\"");
        sb.AppendLine("{");
        sb.AppendLine("    Caption = '" + safeName.Replace("'", "''") + "';");
        sb.AppendLine("    UsageCategory = ReportsAndAnalysis;");
        sb.AppendLine("    ApplicationArea = All;");
        sb.AppendLine();
        sb.AppendLine("    dataset");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    requestpage");
        sb.AppendLine("    {");
        sb.AppendLine("        layout");
        sb.AppendLine("        {");
        sb.AppendLine("            area(Content)");
        sb.AppendLine("            {");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    rendering");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
        return fullPath;
    }

    public static string CreateLayout(string fullPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string ext = Path.GetExtension(fullPath);

        if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            CreateBlankExcel(fullPath);
            return fullPath;
        }

        if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            CreateBlankWord(fullPath);
            return fullPath;
        }

        // Default: RDLC / RDL
        File.WriteAllText(fullPath, MinimalRdlc, Encoding.UTF8);
        return fullPath;
    }

    public static void CopyIntoProject(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    public static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "NewReport";

        var sb = new StringBuilder(name.Length);
        foreach (char c in name.Trim())
        {
            if (char.IsLetterOrDigit(c) || c is ' ' or '_' or '-')
                sb.Append(c);
        }

        string cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "NewReport" : cleaned;
    }

    private static void CreateBlankExcel(string path)
    {
        using var doc = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = doc.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
        sheetPart.Worksheet = new Worksheet(new SheetData());
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(sheetPart),
            SheetId = 1,
            Name = "Sheet1"
        });
        workbookPart.Workbook.Save();
    }

    private static void CreateBlankWord(string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body(
            new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(
                new DocumentFormat.OpenXml.Wordprocessing.Text("Report layout")))));
        main.Document.Save();
    }

    private const string MinimalRdlc =
"""
<?xml version="1.0" encoding="utf-8"?>
<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"
        xmlns:rd="http://schemas.microsoft.com/SQLServer/reporting/reportdesigner">
  <AutoRefresh>0</AutoRefresh>
  <DataSources />
  <DataSets />
  <ReportSections>
    <ReportSection>
      <Body>
        <ReportItems>
          <Textbox Name="Title">
            <CanGrow>true</CanGrow>
            <KeepTogether>true</KeepTogether>
            <Paragraphs>
              <Paragraph>
                <TextRuns>
                  <TextRun>
                    <Value>New Report Layout</Value>
                    <Style>
                      <FontSize>14pt</FontSize>
                      <FontWeight>Bold</FontWeight>
                    </Style>
                  </TextRun>
                </TextRuns>
                <Style />
              </Paragraph>
            </Paragraphs>
            <Top>0.25in</Top>
            <Left>0.25in</Left>
            <Height>0.4in</Height>
            <Width>4in</Width>
            <Style>
              <Border>
                <Style>None</Style>
              </Border>
              <PaddingLeft>2pt</PaddingLeft>
              <PaddingRight>2pt</PaddingRight>
              <PaddingTop>2pt</PaddingTop>
              <PaddingBottom>2pt</PaddingBottom>
            </Style>
          </Textbox>
        </ReportItems>
        <Height>2in</Height>
        <Style />
      </Body>
      <Width>6.5in</Width>
      <Page>
        <PageHeight>11in</PageHeight>
        <PageWidth>8.5in</PageWidth>
        <LeftMargin>0.5in</LeftMargin>
        <RightMargin>0.5in</RightMargin>
        <TopMargin>0.5in</TopMargin>
        <BottomMargin>0.5in</BottomMargin>
        <Style />
      </Page>
    </ReportSection>
  </ReportSections>
  <ReportParameters />
  <ConsumeContainerWhitespace>true</ConsumeContainerWhitespace>
  <rd:ReportUnitType>Inch</rd:ReportUnitType>
  <rd:ReportID>00000000-0000-0000-0000-000000000001</rd:ReportID>
</Report>
""";
}

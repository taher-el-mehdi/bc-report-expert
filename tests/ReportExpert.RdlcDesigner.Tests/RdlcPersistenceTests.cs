using System.IO;
using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Model;
using ReportExpert.RdlcDesigner.Persistence;
using ReportExpert.RdlcDesigner.Services;

namespace ReportExpert.RdlcDesigner.Tests;

public class RdlcPersistenceTests
{
    private static readonly string SampleRdlc =
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
            <Paragraphs>
              <Paragraph>
                <TextRuns>
                  <TextRun>
                    <Value>Hello</Value>
                    <Style />
                  </TextRun>
                </TextRuns>
                <Style />
              </Paragraph>
            </Paragraphs>
            <Top>0.25in</Top>
            <Left>0.25in</Left>
            <Height>0.4in</Height>
            <Width>4in</Width>
            <Style />
          </Textbox>
          <Rectangle Name="Box1">
            <Top>1in</Top>
            <Left>0.5in</Left>
            <Height>1in</Height>
            <Width>2in</Width>
            <Style />
          </Rectangle>
        </ReportItems>
        <Height>3in</Height>
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
  <rd:ReportID>11111111-1111-1111-1111-111111111111</rd:ReportID>
</Report>
""";

    [Fact]
    public void Load_Save_NoEdits_PreservesEditableSemantics()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var persistence = new RdlcPersistence();
            RdlcDocument doc = persistence.Load(path);
            Assert.Equal(2, doc.Items.Count);

            string outPath = path + ".out.rdlc";
            persistence.Save(doc, outPath);

            RdlcDocument again = persistence.Load(outPath);
            Assert.Equal(doc.Items.Count, again.Items.Count);

            var title = again.FindModel("Title");
            Assert.NotNull(title);
            Assert.Equal("Hello", title!.Value);
            Assert.Equal(RdlUnits.ToPx("0.25in"), title.LeftPx, 3);
            Assert.Equal(RdlUnits.ToPx("4in"), title.WidthPx, 3);

            // Designer namespace / ReportID preserved
            XNamespace rd = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";
            Assert.NotNull(again.Xml.Root?.Element(rd + "ReportID"));
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".out.rdlc");
        }
    }

    [Fact]
    public void GeometryAndValue_Edits_RoundTrip()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var persistence = new RdlcPersistence();
            RdlcDocument doc = persistence.Load(path);
            ReportItemModel title = doc.FindModel("Title")!;
            ReportItemModel box = doc.FindModel("Box1")!;

            title.ApplyGeometry(48, 48, 200, 40);
            title.ApplyValue("Updated");
            box.ApplyGeometry(96, 120, 150, 80);

            string outPath = path + ".edit.rdlc";
            persistence.Save(doc, outPath);

            RdlcDocument again = persistence.Load(outPath);
            Assert.Equal("Updated", again.FindModel("Title")!.Value);
            Assert.Equal(48, again.FindModel("Title")!.LeftPx, 2);
            Assert.Equal(200, again.FindModel("Title")!.WidthPx, 2);
            Assert.Equal(96, again.FindModel("Box1")!.LeftPx, 2);
            Assert.Equal(80, again.FindModel("Box1")!.HeightPx, 2);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + ".edit.rdlc");
        }
    }

    [Fact]
    public void UndoService_RestoresGeometry()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var doc = new RdlcPersistence().Load(path);
            var item = doc.FindModel("Title")!;
            double oldL = item.LeftPx, oldT = item.TopPx, oldW = item.WidthPx, oldH = item.HeightPx;

            var undo = new UndoService();
            undo.Execute(new SetGeometryCommand(item, oldL, oldT, oldW, oldH, 10, 20, 30, 40));
            Assert.Equal(10, item.LeftPx, 2);

            undo.Undo();
            Assert.Equal(oldL, item.LeftPx, 2);
            undo.Redo();
            Assert.Equal(10, item.LeftPx, 2);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string WriteTemp(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"re-rdlc-{Guid.NewGuid():N}.rdlc");
        File.WriteAllText(path, content);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}

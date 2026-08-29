using System.IO;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Extensibility;
using ReportExpert.RdlcDesigner.Persistence;
using ReportExpert.RdlcDesigner.Services;

namespace ReportExpert.RdlcDesigner.Tests;

public class SchemaAndEditTests
{
    private static readonly string SampleRdlc =
"""
<?xml version="1.0" encoding="utf-8"?>
<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"
        xmlns:rd="http://schemas.microsoft.com/SQLServer/reporting/reportdesigner">
  <DataSources />
  <DataSets>
    <DataSet Name="DataSet_Result">
      <Fields>
        <Field Name="No">
          <DataField>No</DataField>
          <rd:TypeName>System.String</rd:TypeName>
        </Field>
        <Field Name="Name">
          <DataField>Name</DataField>
          <rd:TypeName>System.String</rd:TypeName>
        </Field>
      </Fields>
      <Query><DataSourceName>DataSource</DataSourceName><CommandText /></Query>
    </DataSet>
  </DataSets>
  <ReportSections>
    <ReportSection>
      <Body>
        <ReportItems />
        <Height>2in</Height>
        <Style />
      </Body>
      <Width>6.5in</Width>
      <Page>
        <PageHeight>11in</PageHeight>
        <PageWidth>8.5in</PageWidth>
        <Style />
      </Page>
    </ReportSection>
  </ReportSections>
  <ReportParameters>
    <ReportParameter Name="ShowDetails">
      <DataType>Boolean</DataType>
      <Nullable>true</Nullable>
      <AllowBlank>true</AllowBlank>
      <Prompt>Show details</Prompt>
    </ReportParameter>
  </ReportParameters>
</Report>
""";

    [Fact]
    public void SchemaService_ReadsDatasetsAndParameters()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var doc = new RdlcPersistence().Load(path);
            var schema = new SchemaService(() => doc);
            schema.Refresh();

            Assert.Single(schema.DataSets);
            Assert.Equal("DataSet_Result", schema.DataSets[0].Name);
            Assert.Equal(2, schema.DataSets[0].Fields.Count);
            Assert.Single(schema.Parameters);
            Assert.Equal("ShowDetails", schema.Parameters[0].Name);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void DocumentEditService_InsertsLineImageAndFieldTextbox()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var doc = new RdlcPersistence().Load(path);
            var undo = new UndoService();
            var selection = new SelectionService();
            var properties = new PropertyService(() => doc, undo);
            var edits = new DocumentEditService(() => doc, selection, properties, undo);

            Assert.NotNull(edits.Insert(ToolboxItemKind.Line, 10, 10));
            Assert.NotNull(edits.Insert(ToolboxItemKind.Image, 20, 20));
            Assert.NotNull(edits.InsertFieldTextbox("DataSet_Result", "No", 30, 30));

            doc.RebuildIndex();
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Line);
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Image);
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Textbox && i.Value == "=Fields!No.Value");
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void DocumentEditService_InsertsTablixChartSubreportAndGauge()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var doc = new RdlcPersistence().Load(path);
            var undo = new UndoService();
            var selection = new SelectionService();
            var properties = new PropertyService(() => doc, undo);
            var edits = new DocumentEditService(() => doc, selection, properties, undo);

            Assert.NotNull(edits.Insert(ToolboxItemKind.Table, 10, 10));
            Assert.NotNull(edits.Insert(ToolboxItemKind.Chart, 20, 20));
            Assert.NotNull(edits.Insert(ToolboxItemKind.Subreport, 30, 30));
            Assert.NotNull(edits.Insert(ToolboxItemKind.Gauge, 40, 40));

            doc.RebuildIndex();
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Tablix);
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Chart);
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Subreport);
            Assert.Contains(doc.Items, i => i.Kind == ReportItemKind.Gauge);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void PropertyBrowser_UsesKindSpecificProviders()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var doc = new RdlcPersistence().Load(path);
            var undo = new UndoService();
            var selection = new SelectionService();
            var properties = new PropertyService(() => doc, undo);
            var edits = new DocumentEditService(() => doc, selection, properties, undo);
            var browser = new ReportExpert.RdlcDesigner.Properties.PropertyBrowserService(
                () => doc, selection, properties);

            Assert.Equal("Report", browser.GetSelectionCaption());
            Assert.Empty(browser.GetCategories());

            IReportItem? textbox = edits.Insert(ToolboxItemKind.TextBox, 5, 5);
            Assert.NotNull(textbox);
            Assert.Contains(browser.GetCategories(), c => c.Name == "TextBox");
            Assert.DoesNotContain(browser.GetCategories(), c => c.Name == "Image");

            IReportItem? image = edits.Insert(ToolboxItemKind.Image, 15, 15);
            Assert.NotNull(image);
            Assert.Contains(browser.GetCategories(), c => c.Name == "Image");
            Assert.DoesNotContain(browser.GetCategories(), c => c.Name == "TextBox");
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void ImageProperty_AppliesExternalFileUriWithoutMimeType()
    {
        string path = WriteTemp(SampleRdlc);
        string imagePath = Path.Combine(Path.GetTempPath(), $"re-img-{Guid.NewGuid():N}.png");
        try
        {
            // Minimal 1x1 PNG
            File.WriteAllBytes(imagePath,
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
                0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
                0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59, 0xE7, 0x00, 0x00,
                0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            ]);

            var doc = new RdlcPersistence().Load(path);
            var undo = new UndoService();
            var selection = new SelectionService();
            var properties = new PropertyService(() => doc, undo);
            var edits = new DocumentEditService(() => doc, selection, properties, undo);

            IReportItem? image = edits.Insert(ToolboxItemKind.Image, 10, 10);
            Assert.NotNull(image);

            string? normalized = ReportExpert.RdlcDesigner.Properties.Providers.ImagePropertyProvider
                .NormalizeExternalPath(imagePath);
            Assert.NotNull(normalized);
            Assert.StartsWith("file:///", normalized, StringComparison.OrdinalIgnoreCase);

            properties.SetImage(image!, "Embedded", normalized, "image/png");
            image = doc.Find(image!.Id);
            Assert.NotNull(image);
            Assert.Equal("External", image!.ImageSource);
            Assert.Equal(normalized, image.ImageValue);
            Assert.Null(image.ImageMimeType);

            var browser = new ReportExpert.RdlcDesigner.Properties.PropertyBrowserService(
                () => doc, selection, properties);
            selection.Select(image);
            Assert.Contains(browser.GetCategories().SelectMany(c => c.Properties), p => p.Key == "ImageValue");
            Assert.DoesNotContain(browser.GetCategories().SelectMany(c => c.Properties), p => p.Key == "MIMEType");
            Assert.Contains(browser.GetCategories().SelectMany(c => c.Properties),
                p => p.Key == "ImageSource" && Equals(p.Value, "External"));
        }
        finally
        {
            TryDelete(path);
            TryDelete(imagePath);
        }
    }

    [Fact]
    public void SchemaService_AddUpdateRemoveParameter()
    {
        string path = WriteTemp(SampleRdlc);
        try
        {
            var doc = new RdlcPersistence().Load(path);
            var schema = new SchemaService(() => doc);
            schema.Refresh();

            Assert.NotNull(schema.AddParameter("CustomerNo"));
            schema.UpdateParameter("CustomerNo", "String", true, true, "Customer", "10000");
            schema.Refresh();
            RdlcParameterNode? p = schema.Parameters.FirstOrDefault(x => x.Name == "CustomerNo");
            Assert.NotNull(p);
            Assert.Equal("Customer", p!.Prompt);
            Assert.Equal("10000", p.DefaultValue);

            Assert.True(schema.RemoveParameter("CustomerNo"));
            Assert.DoesNotContain(schema.Parameters, x => x.Name == "CustomerNo");
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

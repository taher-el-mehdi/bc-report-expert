using System.IO;
using System.Windows.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ReportExpert.Modules.Preview.Services;
using WpfParagraph = System.Windows.Documents.Paragraph;
using WpfRun = System.Windows.Documents.Run;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WordRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WordTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WordTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WordTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace ReportExpert.Modules.Preview.Tests;

public class DocxFlowDocumentServiceTests
{
    private readonly DocxFlowDocumentService _sut = new();

    [Fact]
    public void LoadWithSummary_MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _sut.LoadWithSummary(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.docx")));
    }

    [Fact]
    public void LoadWithSummary_SimpleParagraph_BuildsFlowDocumentAndSummary()
    {
        string path = CreateTempDocx(body =>
        {
            body.AppendChild(new WordParagraph(
                new WordRun(new WordText("Hello Report Expert"))));
        });

        try
        {
            (FlowDocument document, string summary) = _sut.LoadWithSummary(path);

            Assert.Contains("1 paragraph(s)", summary, StringComparison.Ordinal);
            Assert.DoesNotContain("table", summary, StringComparison.OrdinalIgnoreCase);

            string text = string.Concat(
                document.Blocks.OfType<WpfParagraph>()
                    .SelectMany(p => p.Inlines.OfType<WpfRun>())
                    .Select(r => r.Text));

            Assert.Contains("Hello Report Expert", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithSummary_Table_IncludesTableInSummary()
    {
        string path = CreateTempDocx(body =>
        {
            var table = new WordTable(
                new WordTableRow(
                    new WordTableCell(new WordParagraph(new WordRun(new WordText("A")))),
                    new WordTableCell(new WordParagraph(new WordRun(new WordText("B"))))));
            body.AppendChild(table);
        });

        try
        {
            (_, string summary) = _sut.LoadWithSummary(path);
            Assert.Contains("1 table(s)", summary, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithSummary_EmptyBody_ShowsPlaceholder()
    {
        string path = CreateTempDocx(_ => { });

        try
        {
            (FlowDocument document, _) = _sut.LoadWithSummary(path);
            string text = string.Concat(
                document.Blocks.OfType<WpfParagraph>()
                    .SelectMany(p => p.Inlines.OfType<WpfRun>())
                    .Select(r => r.Text));

            Assert.Contains("No visible text", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithSummary_SampleBcInvoice_DoesNotThrow()
    {
        string sample = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "test-reports", "StandardSalesInvoice.docx"));

        if (!File.Exists(sample))
            sample = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "test-reports", "StandardSalesInvoice.docx"));

        if (!File.Exists(sample))
            return; // optional sample asset

        (FlowDocument document, string summary) = _sut.LoadWithSummary(sample);

        Assert.Contains("content control", summary, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(document.Blocks);
    }

    private static string CreateTempDocx(Action<Body> buildBody)
    {
        string path = Path.Combine(Path.GetTempPath(), $"re-docx-{Guid.NewGuid():N}.docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        MainDocumentPart main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body());
        buildBody(main.Document.Body!);
        main.Document.Save();
        return path;
    }
}

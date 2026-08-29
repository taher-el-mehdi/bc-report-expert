using ReportExpert.Modules.Preview.Export;

namespace ReportExpert.Modules.Preview.Tests;

public class ExportFormatTests
{
    [Theory]
    [InlineData(ExportFormat.Pdf, "PDF", ".pdf", "PDF")]
    [InlineData(ExportFormat.Excel, "EXCELOPENXML", ".xlsx", "Excel")]
    [InlineData(ExportFormat.Word, "WORDOPENXML", ".docx", "Word")]
    [InlineData(ExportFormat.Image, "IMAGE", ".tif", "Image (TIFF)")]
    public void Extensions_MatchReportViewerRenderNames(
        ExportFormat format,
        string renderFormat,
        string extension,
        string displayName)
    {
        Assert.Equal(renderFormat, format.RenderFormat());
        Assert.Equal(extension, format.DefaultExtension());
        Assert.Equal(displayName, format.DisplayName());
    }
}

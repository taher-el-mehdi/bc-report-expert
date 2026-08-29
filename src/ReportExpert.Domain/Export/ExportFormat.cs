namespace ReportExpert.Domain.Export;

public enum ExportFormat
{
    Pdf,
    Excel,
    Word,
    Image
}

public static class ExportFormatExtensions
{
    public static string RenderFormat(this ExportFormat format) => format switch
    {
        ExportFormat.Pdf => "PDF",
        ExportFormat.Excel => "EXCELOPENXML",
        ExportFormat.Word => "WORDOPENXML",
        ExportFormat.Image => "IMAGE",
        _ => "PDF"
    };

    public static string DefaultExtension(this ExportFormat format) => format switch
    {
        ExportFormat.Pdf => ".pdf",
        ExportFormat.Excel => ".xlsx",
        ExportFormat.Word => ".docx",
        ExportFormat.Image => ".tif",
        _ => ".pdf"
    };

    public static string DisplayName(this ExportFormat format) => format switch
    {
        ExportFormat.Pdf => "PDF",
        ExportFormat.Excel => "Excel",
        ExportFormat.Word => "Word",
        ExportFormat.Image => "Image (TIFF)",
        _ => "PDF"
    };
}

using System.Data;
using Microsoft.Reporting.NETCore;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Export;

namespace ReportExpert.Reporting.Export.Providers;

public abstract class RenderExportProviderBase : IExportProvider
{
    public abstract ExportFormat Format { get; }
    public abstract string DisplayName { get; }
    public abstract string FileExtension { get; }

    public Task ExportAsync(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        IDictionary<string, object?> parameters,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var report = new LocalReport();
            ReportBindingHelper.LoadDefinition(report, rdlcPath);
            ReportBindingHelper.BindDataSources(report, tables);
            ReportBindingHelper.BindParameters(report, parameters);
            byte[] bytes = report.Render(Format.RenderFormat());
            ReportBindingHelper.WriteOutput(destinationPath, bytes);
        }, cancellationToken);
}

public sealed class PdfExportProvider : RenderExportProviderBase
{
    public override ExportFormat Format => ExportFormat.Pdf;
    public override string DisplayName => "PDF";
    public override string FileExtension => ".pdf";
}

public sealed class ExcelExportProvider : RenderExportProviderBase
{
    public override ExportFormat Format => ExportFormat.Excel;
    public override string DisplayName => "Excel";
    public override string FileExtension => ".xlsx";
}

public sealed class WordExportProvider : RenderExportProviderBase
{
    public override ExportFormat Format => ExportFormat.Word;
    public override string DisplayName => "Word";
    public override string FileExtension => ".docx";
}

public sealed class ImageExportProvider : RenderExportProviderBase
{
    public override ExportFormat Format => ExportFormat.Image;
    public override string DisplayName => "Image (TIFF)";
    public override string FileExtension => ".tif";
}

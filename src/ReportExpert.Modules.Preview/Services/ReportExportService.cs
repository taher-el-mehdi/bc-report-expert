using System.Data;
using System.IO;
using Microsoft.Reporting.NETCore;
using ReportExpert.Modules.Preview.Export;

namespace ReportExpert.Modules.Preview.Services;

public sealed class ReportExportService
{
    public Task ExportAsync(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        IDictionary<string, object?> parameters,
        ExportFormat format,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var report = new LocalReport();
            using FileStream rdlcStream = File.OpenRead(rdlcPath);
            report.EnableHyperlinks = true;
            report.EnableExternalImages = true;
            report.LoadReportDefinition(rdlcStream);
            report.EnableHyperlinks = true;
            report.EnableExternalImages = true;

            BindDataSources(report, tables);
            BindParameters(report, parameters);

            byte[] bytes = report.Render(format.RenderFormat());

            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(destinationPath, bytes);
        }, cancellationToken);
    }

    private static void BindDataSources(LocalReport report, IReadOnlyList<DataTable> tables)
    {
        report.DataSources.Clear();
        foreach (var table in tables)
            report.DataSources.Add(new ReportDataSource(table.TableName, table));
    }

    private static void BindParameters(LocalReport report, IDictionary<string, object?> parameters)
    {
        if (parameters.Count == 0)
            return;

        var reportParams = parameters
            .Select(p => new ReportParameter(p.Key, p.Value?.ToString() ?? string.Empty))
            .ToArray();

        report.SetParameters(reportParams);
    }
}

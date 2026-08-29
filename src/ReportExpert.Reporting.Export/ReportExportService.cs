using System.Data;
using System.IO;
using Microsoft.Reporting.NETCore;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Export;
using ReportParameter = Microsoft.Reporting.NETCore.ReportParameter;

namespace ReportExpert.Reporting.Export;

public sealed class ReportExportService : IReportExportService
{
    private readonly IEnumerable<IExportProvider> _providers;

    public ReportExportService(IEnumerable<IExportProvider> providers) =>
        _providers = providers;

    public Task ExportAsync(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        IDictionary<string, object?> parameters,
        ExportFormat format,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.Format == format)
            ?? throw new NotSupportedException($"Export format '{format}' is not supported.");

        return provider.ExportAsync(rdlcPath, tables, parameters, destinationPath, cancellationToken);
    }
}

internal static class ReportBindingHelper
{
    public static void BindDataSources(LocalReport report, IReadOnlyList<DataTable> tables)
    {
        report.DataSources.Clear();
        foreach (var table in tables)
            report.DataSources.Add(new ReportDataSource(table.TableName, table));
    }

    public static void BindParameters(LocalReport report, IDictionary<string, object?> parameters)
    {
        if (parameters.Count == 0)
            return;

        var reportParams = parameters
            .Select(p => new ReportParameter(p.Key, p.Value?.ToString() ?? string.Empty))
            .ToArray();

        report.SetParameters(reportParams);
    }

    public static void LoadDefinition(LocalReport report, string rdlcPath)
    {
        using FileStream rdlcStream = File.OpenRead(rdlcPath);
        report.LoadReportDefinition(rdlcStream);
    }

    public static void WriteOutput(string destinationPath, byte[] bytes)
    {
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(destinationPath, bytes);
    }
}

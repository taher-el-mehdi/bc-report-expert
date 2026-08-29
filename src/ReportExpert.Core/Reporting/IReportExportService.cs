using System.Data;
using ReportExpert.Domain.Export;

namespace ReportExpert.Core.Reporting;

public interface IReportExportService
{
    Task ExportAsync(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        IDictionary<string, object?> parameters,
        ExportFormat format,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

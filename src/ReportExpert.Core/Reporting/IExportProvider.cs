using System.Data;
using ReportExpert.Domain.Export;

namespace ReportExpert.Core.Reporting;

public interface IExportProvider
{
    ExportFormat Format { get; }
    string DisplayName { get; }
    string FileExtension { get; }
    Task ExportAsync(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        IDictionary<string, object?> parameters,
        string destinationPath,
        CancellationToken cancellationToken = default);
}

using System.Data;
using ReportExpert.Core.Configuration;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Models;

namespace ReportExpert.Application.Reports.DummyData;

public sealed class GenerateDummyDataRequest
{
    public required RdlcReportMetadata Metadata { get; init; }
    public int RowCount { get; init; }
}

public sealed class GenerateDummyDataResult
{
    public required IReadOnlyDictionary<string, DataTable> Tables { get; init; }
}

public sealed class GenerateDummyDataUseCase
{
    private readonly IDummyDataGenerator _dataGenerator;
    private readonly ISettingsService _settingsService;

    public GenerateDummyDataUseCase(IDummyDataGenerator dataGenerator, ISettingsService settingsService)
    {
        _dataGenerator = dataGenerator;
        _settingsService = settingsService;
    }

    public Task<GenerateDummyDataResult> ExecuteAsync(GenerateDummyDataRequest request, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var tables = _dataGenerator.GenerateAll(request.Metadata.DataSets, request.RowCount);
            var dictionary = tables.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);
            _settingsService.Settings.RowsGenerated = request.RowCount;
            _settingsService.Save();
            return new GenerateDummyDataResult { Tables = dictionary };
        }, cancellationToken);
}

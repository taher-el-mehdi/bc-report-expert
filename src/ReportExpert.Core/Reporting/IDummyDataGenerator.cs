using System.Data;
using ReportExpert.Domain.Models;

namespace ReportExpert.Core.Reporting;

public interface IDummyDataGenerator
{
    List<DataTable> GenerateAll(IReadOnlyList<RdlcDataSetInfo> datasets, int rowCount);
    DataTable GenerateTable(RdlcDataSetInfo dataset, int rowCount);
    object? GenerateValue(RdlcFieldInfo field, int rowIndex);
    string BuildSchemaText(RdlcDataSetInfo dataset);
    string ExportTableToJson(DataTable table);
    string ExportTableToXml(DataTable table);
    string ExportTableToCsv(DataTable table);
}

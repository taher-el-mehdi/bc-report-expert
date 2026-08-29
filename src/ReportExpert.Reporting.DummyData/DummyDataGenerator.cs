using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Models;

namespace ReportExpert.Reporting.DummyData;

public sealed class DummyDataGenerator : IDummyDataGenerator
{
    private readonly Random _random = new();

    private static readonly string[] FirstNames =
        ["John", "Jane", "Michael", "Sarah", "David", "Emily", "Robert", "Lisa", "James", "Anna"];

    private static readonly string[] LastNames =
        ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Wilson", "Taylor"];

    private static readonly string[] Companies =
        ["Contoso Ltd", "Fabrikam Inc", "Northwind Traders", "Adventure Works", "Tailspin Toys"];

    private static readonly string[] Cities =
        ["Seattle", "London", "Paris", "Berlin", "Tokyo", "Sydney", "Toronto", "Amsterdam"];

    private static readonly string[] Countries =
        ["United States", "United Kingdom", "France", "Germany", "Japan", "Australia", "Canada"];

    private static readonly string[] Statuses =
        ["Active", "Pending", "Closed", "Draft", "Approved"];

    public List<DataTable> GenerateAll(IReadOnlyList<RdlcDataSetInfo> datasets, int rowCount) =>
        datasets.Select(ds => GenerateTable(ds, rowCount)).ToList();

    public DataTable GenerateTable(RdlcDataSetInfo dataset, int rowCount)
    {
        var table = new DataTable(dataset.Name);

        foreach (var field in dataset.Fields)
            table.Columns.Add(field.DataField, Nullable.GetUnderlyingType(field.ClrType) ?? field.ClrType);

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = table.NewRow();
            foreach (var field in dataset.Fields)
                row[field.DataField] = GenerateValue(field, rowIndex) ?? DBNull.Value;
            table.Rows.Add(row);
        }

        return table;
    }

    public object? GenerateValue(RdlcFieldInfo field, int rowIndex)
    {
        var clrType = Nullable.GetUnderlyingType(field.ClrType) ?? field.ClrType;
        string name = field.Name;
        string dataField = field.DataField;

        if (clrType == typeof(bool))
            return InferBoolean(name, dataField, rowIndex);

        if (clrType == typeof(int) || clrType == typeof(short) || clrType == typeof(long))
            return InferInteger(name, dataField, rowIndex, clrType);

        if (clrType == typeof(decimal) || clrType == typeof(double) || clrType == typeof(float))
            return InferDecimal(name, dataField, rowIndex, clrType);

        if (clrType == typeof(DateTime))
            return InferDate(name, dataField, rowIndex);

        if (clrType == typeof(Guid))
            return Guid.NewGuid();

        if (clrType == typeof(byte[]))
            return Encoding.UTF8.GetBytes($"sample-{rowIndex}");

        return InferString(name, dataField, rowIndex);
    }

    public string BuildSchemaText(RdlcDataSetInfo dataset)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Dataset: {dataset.Name}");
        sb.AppendLine("Fields:");
        foreach (var field in dataset.Fields)
            sb.AppendLine($"  {field.Name} ({field.TypeName})");
        return sb.ToString();
    }

    public string ExportTableToJson(DataTable table)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in table.Rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (DataColumn col in table.Columns)
                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            rows.Add(dict);
        }

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ExportTableToXml(DataTable table)
    {
        var root = new XElement("DataSet", new XAttribute("Name", table.TableName));
        foreach (DataRow row in table.Rows)
        {
            var rowEl = new XElement("Row");
            foreach (DataColumn col in table.Columns)
            {
                object value = row[col];
                rowEl.Add(new XElement(col.ColumnName, value == DBNull.Value ? string.Empty : value));
            }
            root.Add(rowEl);
        }

        return root.ToString();
    }

    public string ExportTableToCsv(DataTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(c.ColumnName))));
        foreach (DataRow row in table.Rows)
        {
            var values = table.Columns.Cast<DataColumn>()
                .Select(c => EscapeCsv(FormatCsvValue(row[c])));
            sb.AppendLine(string.Join(",", values));
        }
        return sb.ToString();
    }

    private bool InferBoolean(string name, string dataField, int rowIndex)
    {
        if (ContainsAny(name, dataField, "active", "enabled", "is", "has", "flag"))
            return rowIndex % 2 == 0;
        return _random.Next(0, 2) == 0;
    }

    private object InferInteger(string name, string dataField, int rowIndex, Type clrType)
    {
        if (ContainsAny(name, dataField, "quantity", "qty", "count", "number", "no", "id", "line"))
        {
            int value = rowIndex + 1 + _random.Next(0, 100);
            return ConvertToIntegerType(value, clrType);
        }

        return ConvertToIntegerType(100 + rowIndex + _random.Next(0, 900), clrType);
    }

    private object InferDecimal(string name, string dataField, int rowIndex, Type clrType)
    {
        decimal value;
        if (ContainsAny(name, dataField, "discount", "vat", "tax", "rate", "percent"))
            value = Math.Round((decimal)_random.NextDouble() * 25, 2);
        else if (ContainsAny(name, dataField, "price", "amount", "total", "cost", "balance"))
            value = Math.Round(10m + rowIndex * 15.5m + _random.Next(0, 5000) / 100m, 2);
        else
            value = Math.Round((decimal)_random.NextDouble() * 1000, 2);

        return clrType == typeof(double) ? (double)value :
            clrType == typeof(float) ? (float)value : value;
    }

    private DateTime InferDate(string name, string dataField, int rowIndex)
    {
        if (ContainsAny(name, dataField, "birth"))
            return DateTime.Today.AddYears(-25 - rowIndex).AddDays(_random.Next(0, 365));

        if (ContainsAny(name, dataField, "created", "modified", "updated"))
            return DateTime.Today.AddDays(-rowIndex - _random.Next(0, 30));

        return DateTime.Today.AddDays(-rowIndex);
    }

    private string InferString(string name, string dataField, int rowIndex)
    {
        string combined = $"{name} {dataField}".ToLowerInvariant();
        string first = FirstNames[rowIndex % FirstNames.Length];
        string last = LastNames[(rowIndex + 3) % LastNames.Length];

        if (combined.Contains("firstname") || combined.Contains("first_name"))
            return first;
        if (combined.Contains("lastname") || combined.Contains("last_name"))
            return last;
        if (combined.Contains("customername") || combined.Contains("contactname") ||
            (combined.Contains("name") && !combined.Contains("filename") && !combined.Contains("username")))
            return $"{first} {last}";
        if (combined.Contains("company"))
            return Companies[rowIndex % Companies.Length];
        if (combined.Contains("email"))
            return $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{rowIndex}@example.com";
        if (combined.Contains("phone") || combined.Contains("mobile") || combined.Contains("fax"))
            return $"+1 555 {_random.Next(100, 999):000} {_random.Next(1000, 9999):0000}";
        if (combined.Contains("address") || combined.Contains("street"))
            return $"{100 + rowIndex} Main Street";
        if (combined.Contains("city"))
            return Cities[rowIndex % Cities.Length];
        if (combined.Contains("country"))
            return Countries[rowIndex % Countries.Length];
        if (combined.Contains("zip") || combined.Contains("postal"))
            return $"{10000 + rowIndex}";
        if (combined.Contains("description") || combined.Contains("comment") || combined.Contains("note"))
            return $"Sample description for record {rowIndex + 1}";
        if (combined.Contains("currency"))
            return rowIndex % 2 == 0 ? "USD" : "EUR";
        if (combined.Contains("status"))
            return Statuses[rowIndex % Statuses.Length];
        if (combined.Contains("invoice") || combined.Contains("order") || combined.Contains("document") ||
            combined.Contains("customerno") || combined.Contains("itemno") || combined.Contains("code"))
            return $"DOC-{DateTime.Today:yyyyMMdd}-{rowIndex + 1:D4}";
        if (combined.Contains("guid") || combined.EndsWith("id"))
            return Guid.NewGuid().ToString();

        return $"Sample_{dataField}_{rowIndex + 1}";
    }

    private static bool ContainsAny(string name, string dataField, params string[] tokens)
    {
        string combined = $"{name} {dataField}".ToLowerInvariant();
        return tokens.Any(combined.Contains);
    }

    private static object ConvertToIntegerType(int value, Type clrType) => clrType switch
    {
        _ when clrType == typeof(short) => (short)value,
        _ when clrType == typeof(long) => (long)value,
        _ => value
    };

    private static string FormatCsvValue(object value) =>
        value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

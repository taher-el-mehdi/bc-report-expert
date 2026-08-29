using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>Imports XML, JSON, or CSV data files matching an RDLC dataset schema.</summary>
public sealed class ReportDataImporter
{
    /// <summary>
    /// Same-name sidecar convention used by the VS Code RDL preview extension:
    /// <c>Invoice.rdlc</c> → <c>Invoice.json</c> beside the report.
    /// </summary>
    public static string? ResolveSiblingJsonPath(string rdlcPath)
    {
        if (string.IsNullOrWhiteSpace(rdlcPath))
            return null;

        string jsonPath = Path.ChangeExtension(rdlcPath, ".json");
        return File.Exists(jsonPath) ? jsonPath : null;
    }

    public DataTable Import(string filePath, RdlcDataSetInfo dataset)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => ImportJson(filePath, dataset),
            ".xml" => ImportXml(filePath, dataset),
            ".csv" => ImportCsv(filePath, dataset),
            _ => throw new NotSupportedException($"Unsupported import format: {ext}")
        };
    }

    /// <summary>
    /// Loads every dataset from a sibling JSON file. Supports:
    /// <list type="bullet">
    /// <item>Root array of row objects (bound to the best-matching / only dataset)</item>
    /// <item><c>{ "rows": [ ... ] }</c> (same as array)</item>
    /// <item><c>{ "DatasetName": [ ... ], ... }</c> (per-dataset arrays)</item>
    /// </list>
    /// Datasets not present in the file are omitted from the result.
    /// </summary>
    public Dictionary<string, DataTable> ImportAllFromJson(
        string jsonPath,
        IReadOnlyList<RdlcDataSetInfo> datasets)
    {
        string json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
        if (datasets.Count == 0)
            return result;

        JsonElement root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            bool anyNamed = false;
            foreach (var dataset in datasets)
            {
                if (!root.TryGetProperty(dataset.Name, out var dsEl) || dsEl.ValueKind != JsonValueKind.Array)
                    continue;

                result[dataset.Name] = BuildTable(dataset, ReadRowObjects(dsEl));
                anyNamed = true;
            }

            if (anyNamed)
                return result;

            if (root.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
            {
                BindSharedRows(datasets, ReadRowObjects(rowsEl), result);
                return result;
            }
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            BindSharedRows(datasets, ReadRowObjects(root), result);
            return result;
        }

        if (root.ValueKind == JsonValueKind.Object && datasets.Count == 1)
        {
            result[datasets[0].Name] = BuildTable(datasets[0], [FlattenJsonObject(root)]);
        }

        return result;
    }

    public DataTable ImportJson(string filePath, RdlcDataSetInfo dataset)
    {
        string json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        var rows = new List<Dictionary<string, string?>>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
                rows.Add(FlattenJsonObject(item));
        }
        else if (doc.RootElement.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rowsEl.EnumerateArray())
                rows.Add(FlattenJsonObject(item));
        }
        else if (doc.RootElement.TryGetProperty(dataset.Name, out var dsEl) && dsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dsEl.EnumerateArray())
                rows.Add(FlattenJsonObject(item));
        }
        else
        {
            rows.Add(FlattenJsonObject(doc.RootElement));
        }

        return BuildTable(dataset, rows);
    }

    private static void BindSharedRows(
        IReadOnlyList<RdlcDataSetInfo> datasets,
        List<Dictionary<string, string?>> rows,
        Dictionary<string, DataTable> result)
    {
        if (datasets.Count == 1)
        {
            result[datasets[0].Name] = BuildTable(datasets[0], rows);
            return;
        }

        // Prefer the dataset whose fields overlap the JSON keys the most (BC often has one real set).
        RdlcDataSetInfo? best = null;
        int bestScore = -1;
        var sampleKeys = rows.Count > 0
            ? rows[0].Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataset in datasets)
        {
            int score = dataset.Fields.Count(f =>
                sampleKeys.Contains(f.DataField) || sampleKeys.Contains(f.Name));
            if (score > bestScore)
            {
                bestScore = score;
                best = dataset;
            }
        }

        if (best is null || bestScore <= 0)
            best = datasets[0];

        result[best.Name] = BuildTable(best, rows);
    }

    private static List<Dictionary<string, string?>> ReadRowObjects(JsonElement array)
    {
        var rows = new List<Dictionary<string, string?>>();
        foreach (var item in array.EnumerateArray())
            rows.Add(FlattenJsonObject(item));
        return rows;
    }

    public DataTable ImportXml(string filePath, RdlcDataSetInfo dataset)
    {
        var doc = XDocument.Load(filePath);
        var rowElements = doc.Descendants()
            .Where(e => e.Name.LocalName is "Row" or "row" or "Record" or "record")
            .ToList();

        if (rowElements.Count == 0)
            rowElements = doc.Root?.Elements().ToList() ?? [];

        var rows = rowElements.Select(row =>
            row.Elements().ToDictionary(
                e => e.Name.LocalName,
                e => (string?)e.Value,
                StringComparer.OrdinalIgnoreCase)).ToList();

        return BuildTable(dataset, rows);
    }

    public DataTable ImportCsv(string filePath, RdlcDataSetInfo dataset)
    {
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
            throw new InvalidDataException("CSV file is empty.");

        string[] headers = ParseCsvLine(lines[0]);
        var rows = new List<Dictionary<string, string?>>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] values = ParseCsvLine(lines[i]);
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length; c++)
                dict[headers[c]] = c < values.Length ? values[c] : null;
            rows.Add(dict);
        }

        return BuildTable(dataset, rows);
    }

    private static DataTable BuildTable(RdlcDataSetInfo dataset, IReadOnlyList<Dictionary<string, string?>> rows)
    {
        var table = new DataTable(dataset.Name);
        foreach (var field in dataset.Fields)
            table.Columns.Add(field.DataField, Nullable.GetUnderlyingType(field.ClrType) ?? field.ClrType);

        var warnings = new List<string>();

        foreach (var row in rows)
        {
            var dataRow = table.NewRow();
            foreach (var field in dataset.Fields)
            {
                if (!TryGetValue(row, field, out string? raw))
                {
                    warnings.Add(field.DataField);
                    dataRow[field.DataField] = DBNull.Value;
                    continue;
                }

                dataRow[field.DataField] = ConvertValue(raw, field.ClrType) ?? DBNull.Value;
            }
            table.Rows.Add(dataRow);
        }

        if (warnings.Count > 0 && table.Rows.Count == 0)
            throw new InvalidDataException("No matching fields found in the imported file for this dataset schema.");

        return table;
    }

    private static bool TryGetValue(Dictionary<string, string?> row, RdlcFieldInfo field, out string? value)
    {
        if (row.TryGetValue(field.DataField, out value) && value is not null)
            return true;
        if (row.TryGetValue(field.Name, out value) && value is not null)
            return true;
        value = null;
        return false;
    }

    private static object? ConvertValue(string? raw, Type clrType)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (type == typeof(string)) return raw;
        if (type == typeof(bool)) return bool.Parse(raw);
        if (type == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(short)) return short.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(decimal)) return decimal.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(DateTime)) return DateTime.Parse(raw, CultureInfo.InvariantCulture);
        if (type == typeof(Guid)) return Guid.Parse(raw);
        if (type == typeof(byte[])) return Convert.FromBase64String(raw);

        return raw;
    }

    private static Dictionary<string, string?> FlattenJsonObject(JsonElement element)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();

        return dict;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }
}

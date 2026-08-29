using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Microsoft.Reporting.NETCore;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Renders Business Central RDLC layouts to PDF using BC-exported XML datasources.
/// Ported from the RDLC Report Tester (<c>ReportRenderer</c>) and adapted for Report Expert.
/// </summary>
public static class BcXmlReportRenderer
{
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd",
        "MM/dd/yyyy", "MM-dd-yyyy",
        "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
    ];

    private static int _encodingProviderRegistered;

    private static void EnsureEncodings()
    {
        if (Interlocked.Exchange(ref _encodingProviderRegistered, 1) == 1)
            return;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Parses BC XML, binds RDLC datasets by order, and writes a PDF.</summary>
    public static void RenderToPdf(string rdlcPath, string xmlPath, string outputPath)
    {
        IReadOnlyList<DataTable> tables = ParseDataItems(xmlPath);
        RenderTablesToPdf(rdlcPath, tables, outputPath);
    }

    /// <summary>Binds in-memory tables to the RDLC and writes a PDF.</summary>
    public static void RenderTablesToPdf(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        string outputPath,
        IDictionary<string, object?>? parameters = null)
    {
        string? error = TryRenderTablesToPdf(rdlcPath, tables, outputPath, parameters);
        if (error is not null)
            throw new InvalidOperationException(error);
    }

    /// <summary>
    /// Binds tables and writes a PDF. Returns <c>null</c> on success, or an error message without throwing.
    /// </summary>
    public static string? TryRenderTablesToPdf(
        string rdlcPath,
        IReadOnlyList<DataTable> tables,
        string outputPath,
        IDictionary<string, object?>? parameters = null)
    {
        try
        {
            EnsureEncodings();

            using var report = new LocalReport();
            report.EnableHyperlinks = true;
            report.EnableExternalImages = true;

            // Avoid hanging/failing when a layout references a subreport that isn't available.
            report.SubreportProcessing += (_, e) =>
            {
                e.DataSources.Clear();
                foreach (string dsName in e.DataSourceNames)
                    e.DataSources.Add(new ReportDataSource(dsName, new DataTable(dsName)));
            };

            using (FileStream rdlcStream = File.OpenRead(rdlcPath))
                report.LoadReportDefinition(rdlcStream);

            List<string> dsNames = GetRdlcDataSetNames(rdlcPath);
            var tablesByName = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
            foreach (DataTable table in tables)
            {
                if (!string.IsNullOrWhiteSpace(table.TableName) && !tablesByName.ContainsKey(table.TableName))
                    tablesByName[table.TableName] = table;
            }

            report.DataSources.Clear();
            if (dsNames.Count > 0)
            {
                // Match RDLC Report Tester: bind by RDLC dataset name, falling back to order.
                for (int i = 0; i < dsNames.Count; i++)
                {
                    string dsName = dsNames[i];
                    DataTable table;
                    if (tablesByName.TryGetValue(dsName, out DataTable? named))
                        table = named;
                    else if (i < tables.Count)
                        table = tables[i];
                    else
                        table = new DataTable(dsName);

                    report.DataSources.Add(new ReportDataSource(dsName, table));
                }
            }
            else
            {
                foreach (DataTable table in tables)
                    report.DataSources.Add(new ReportDataSource(table.TableName, table));
            }

            BindParameters(report, parameters);

            byte[] pdf = report.Render("PDF");

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(outputPath, pdf);
            return null;
        }
        catch (LocalProcessingException ex)
        {
            return FormatProcessingError(ex);
        }
        catch (Exception ex)
        {
            return FormatProcessingError(ex);
        }
    }

    private static string FormatProcessingError(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message) &&
                (parts.Count == 0 || !parts[^1].Equals(cur.Message, StringComparison.Ordinal)))
                parts.Add(cur.Message);
        }

        return string.Join(" → ", parts);
    }

    private static void BindParameters(LocalReport report, IDictionary<string, object?>? parameters)
    {
        ReportParameterInfoCollection defined;
        try
        {
            defined = report.GetParameters();
        }
        catch
        {
            // Some definitions throw until datasources are ready; skip optional bind.
            if (parameters is not { Count: > 0 })
                return;

            var fallback = parameters
                .Select(p => new ReportParameter(p.Key, FormatParameterValue(p.Value, null)))
                .ToArray();
            report.SetParameters(fallback);
            return;
        }

        if (defined.Count == 0)
            return;

        var reportParams = new List<ReportParameter>();
        foreach (ReportParameterInfo info in defined)
        {
            object? raw = null;
            bool hasValue = parameters is not null &&
                            parameters.TryGetValue(info.Name, out raw);

            if (!hasValue && info.Values.Count > 0)
                continue; // keep definition default

            string text = FormatParameterValue(raw, info);
            reportParams.Add(new ReportParameter(info.Name, text));
        }

        if (reportParams.Count > 0)
            report.SetParameters(reportParams);
    }

    private static string FormatParameterValue(object? value, ReportParameterInfo? info)
    {
        if (value is bool b)
            return b ? "True" : "False";

        if (value is DateTime dt)
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string text = value?.ToString()?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(text))
            return text;

        string dataType = info?.DataType.ToString() ?? string.Empty;
        return dataType switch
        {
            "Boolean" => "False",
            "Integer" => "0",
            "Float" => "0",
            "DateTime" => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Parses a Business Central report XML export into denormalized <see cref="DataTable"/>s
    /// (typically one flat table named <c>DataSet_Result</c>).
    /// </summary>
    public static List<DataTable> ParseDataItems(string xmlPath)
    {
        XDocument doc = XDocument.Load(xmlPath);

        XElement? dataItemsRoot =
            doc.Root?.Name.LocalName == "DataItems"
                ? doc.Root
                : doc.Root?.Element("DataItems")
                  ?? doc.Descendants("DataItems").FirstOrDefault();

        if (dataItemsRoot is null)
            throw new InvalidDataException("Cannot find <DataItems> element in the XML. Export data from Business Central in the standard DataItems format.");

        var denormRows = new List<List<(string name, bool hasDecimalFmt, string value)>>();
        CollectDenormalizedRows(dataItemsRoot, [], denormRows);

        if (denormRows.Count == 0)
            return [];

        var colOrder = new List<string>();
        var colDecFmt = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in denormRows)
        {
            foreach (var (name, hasDecimalFmt, _) in row)
            {
                if (colDecFmt.ContainsKey(name))
                    continue;

                colDecFmt[name] = hasDecimalFmt;
                colOrder.Add(name);
            }
        }

        var colTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (string colName in colOrder)
        {
            var singleColRows = denormRows
                .Select(r => new List<string> { r.FirstOrDefault(c => c.name == colName).value ?? string.Empty })
                .ToList();
            colTypes[colName] = InferColumnType(singleColRows, 0, colDecFmt[colName]);
        }

        var table = new DataTable("DataSet_Result");
        foreach (string colName in colOrder)
            table.Columns.Add(colName, colTypes[colName]);

        foreach (var rowData in denormRows)
        {
            DataRow row = table.NewRow();
            foreach (var (name, _, value) in rowData)
            {
                if (table.Columns.Contains(name))
                    row[name] = ConvertValue(value, colTypes[name]);
            }

            table.Rows.Add(row);
        }

        return [table];
    }

    /// <summary>
    /// Builds a display-oriented table with typed column headers for the XML inspection grid.
    /// </summary>
    public static DataTable BuildDisplayTable(string xmlPath)
    {
        List<DataTable> tables = ParseDataItems(xmlPath);
        if (tables.Count == 0)
            return new DataTable();

        DataTable source = tables[0];
        var display = new DataTable();
        foreach (DataColumn col in source.Columns)
        {
            string typeName = col.DataType == typeof(int) ? "int"
                : col.DataType == typeof(decimal) ? "decimal"
                : col.DataType == typeof(DateTime) ? "datetime"
                : "string";
            display.Columns.Add($"{col.ColumnName} ({typeName})", typeof(string));
        }

        foreach (DataRow sourceRow in source.Rows)
        {
            DataRow row = display.NewRow();
            for (int i = 0; i < source.Columns.Count; i++)
            {
                object value = sourceRow[i];
                row[i] = value == DBNull.Value ? string.Empty : value.ToString() ?? string.Empty;
            }

            display.Rows.Add(row);
        }

        return display;
    }

    public static List<string> GetRdlcDataSetNames(string rdlcPath)
    {
        XDocument rdlc = XDocument.Load(rdlcPath);
        XNamespace ns = rdlc.Root?.Name.Namespace ?? XNamespace.None;
        return rdlc.Descendants(ns + "DataSet")
            .Select(e => e.Attribute("Name")?.Value ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }

    internal static void CollectDenormalizedRows(
        XElement dataItemsEl,
        List<(string name, bool hasDecimalFmt, string value)> inherited,
        List<List<(string name, bool hasDecimalFmt, string value)>> result)
    {
        foreach (XElement di in dataItemsEl.Elements("DataItem"))
        {
            var ownCols = (di.Element("Columns")?.Elements("Column") ?? [])
                .Select(c => (
                    c.Attribute("name")?.Value ?? "?",
                    c.Attribute("decimalformatter") != null,
                    c.Value))
                .ToList();

            var combined = new List<(string, bool, string)>([.. inherited, .. ownCols]);

            XElement? nested = di.Element("DataItems");
            if (nested is not null && nested.Elements("DataItem").Any())
                CollectDenormalizedRows(nested, combined, result);
            else
                result.Add(combined);
        }
    }

    internal static string InferTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "null";

        Type t = InferColumnType([[value]], 0, false);
        if (t == typeof(int)) return "int";
        if (t == typeof(decimal)) return "decimal";
        if (t == typeof(DateTime)) return "datetime";
        return "string";
    }

    internal static Type InferColumnType(List<List<string>> rows, int colIndex, bool hasDecimalFmt)
    {
        if (hasDecimalFmt)
            return typeof(decimal);

        bool allInt = true;
        bool allDecimal = true;
        bool allDateTime = true;

        foreach (List<string> row in rows)
        {
            string v = colIndex < row.Count ? row[colIndex] : string.Empty;
            if (string.IsNullOrWhiteSpace(v))
                continue;

            if (allInt && !int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                allInt = false;

            if (allDecimal && !decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                allDecimal = false;

            if (allDateTime && !TryParseDate(v, out _))
                allDateTime = false;

            if (!allDecimal && !allDateTime)
                break;
        }

        if (allInt) return typeof(int);
        if (allDecimal) return typeof(decimal);
        if (allDateTime) return typeof(DateTime);
        return typeof(string);
    }

    private static bool TryParseDate(string value, out DateTime result) =>
        DateTime.TryParseExact(
            value,
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);

    private static object ConvertValue(string value, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DBNull.Value;

        if (targetType == typeof(int))
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                ? i
                : DBNull.Value;

        if (targetType == typeof(decimal))
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d)
                ? d
                : DBNull.Value;

        if (targetType == typeof(DateTime))
            return TryParseDate(value, out DateTime dt) ? dt : DBNull.Value;

        return value;
    }
}

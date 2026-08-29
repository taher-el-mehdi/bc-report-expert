using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Parses RDLC XML into <see cref="RdlcReportMetadata"/> — datasets, fields, parameters, and page layout.
/// Supports RDL 2008 and RDL 2016 namespace variants used by Business Central reports.
/// </summary>
public sealed class RdlcMetadataParser
{
    private static readonly XNamespace Rd = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

    private static readonly Regex FieldsValueRegex = new(
        @"Fields!([A-Za-z_][A-Za-z0-9_]*)\.Value",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private RdlcReportMetadata? _cached;

    public RdlcReportMetadata? CachedMetadata => _cached;

    public Task<RdlcReportMetadata> ParseAsync(string rdlcPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Parse(rdlcPath), cancellationToken);
    }

    /// <summary>Reads and parses an RDLC file from disk.</summary>
    public RdlcReportMetadata Parse(string rdlcPath)
    {
        var doc = XDocument.Load(rdlcPath);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var reportEl = doc.Root ?? throw new InvalidDataException("Invalid RDLC file: missing root element.");

        string reportName = Path.GetFileNameWithoutExtension(rdlcPath);
        string reportVersion = reportEl.Attribute("MustUnderstand")?.Value
            ?? doc.Descendants(ns + "Report").FirstOrDefault()?.Attribute("xmlns")?.Value
            ?? "RDLC";

        var pageEl = doc.Descendants(ns + "Page").FirstOrDefault();
        double pageWidth = ParseInches(pageEl?.Element(ns + "PageWidth")?.Value);
        double pageHeight = ParseInches(pageEl?.Element(ns + "PageHeight")?.Value);
        string orientation = pageWidth > pageHeight ? "Landscape" : "Portrait";

        var filterTypeHints = CollectFilterTypeHints(doc, ns);

        var datasets = doc.Descendants(ns + "DataSet")
            .Select(ds =>
            {
                string dsName = ds.Attribute("Name")?.Value ?? "DataSet";
                var fields = ds.Descendants(ns + "Field")
                    .Select(f => CreateField(f, ns, filterTypeHints))
                    .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                    .ToList();

                return new RdlcDataSetInfo
                {
                    Name = dsName,
                    Fields = fields
                };
            })
            .Where(ds => !string.IsNullOrWhiteSpace(ds.Name))
            .ToList();

        var parameters = doc.Descendants(ns + "ReportParameter")
            .Select(p => new RdlcParameterInfo
            {
                Name = p.Attribute("Name")?.Value ?? string.Empty,
                DataType = p.Element(ns + "DataType")?.Value ?? "String",
                Nullable = bool.TryParse(p.Element(ns + "Nullable")?.Value, out bool n) && n,
                AllowBlank = !bool.TryParse(p.Element(ns + "AllowBlank")?.Value, out bool ab) || ab,
                MultiValue = bool.TryParse(p.Element(ns + "MultiValue")?.Value, out bool mv) && mv,
                DefaultValue = p.Descendants(ns + "Value").FirstOrDefault()?.Value,
                ValidValues = p.Descendants(ns + "ParameterValue")
                    .Select(v => v.Element(ns + "Value")?.Value ?? string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList()
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        int imageCount = doc.Descendants(ns + "EmbeddedImage").Count();
        int expressionCount = doc.Descendants(ns + "Value")
            .Count(v => (v.Value ?? string.Empty).TrimStart().StartsWith('='));

        _cached = new RdlcReportMetadata
        {
            FilePath = rdlcPath,
            ReportName = reportName,
            ReportVersion = reportVersion,
            DataSets = datasets,
            Parameters = parameters,
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            Orientation = orientation,
            EmbeddedImageCount = imageCount,
            ExpressionCount = expressionCount
        };

        return _cached;
    }

    public void InvalidateCache() => _cached = null;

    private static RdlcFieldInfo CreateField(
        XElement field,
        XNamespace ns,
        IReadOnlyDictionary<string, Type> filterTypeHints)
    {
        string name = field.Attribute("Name")?.Value ?? string.Empty;
        string dataField = field.Element(ns + "DataField")?.Value
                           ?? field.Elements().FirstOrDefault(e => e.Name.LocalName == "DataField")?.Value
                           ?? name;

        string? typeName = field.Element(Rd + "TypeName")?.Value
                           ?? field.Elements().FirstOrDefault(e => e.Name.LocalName == "TypeName")?.Value;

        Type clrType = ResolveClrType(typeName);

        // Filters like Fields!LineNo.Value > 0 require numeric columns even when TypeName is missing/wrong.
        if (filterTypeHints.TryGetValue(name, out Type? hint) ||
            filterTypeHints.TryGetValue(dataField, out hint))
        {
            clrType = PreferMoreSpecific(clrType, hint);
        }

        if (clrType == typeof(string))
            clrType = InferTypeFromName(name, dataField);

        return new RdlcFieldInfo
        {
            Name = name,
            DataField = dataField,
            TypeName = clrType.FullName ?? typeName ?? "System.String",
            ClrType = clrType
        };
    }

    /// <summary>
    /// Reads tablix/group filters: Fields!X.Value compared to =0 / 2 / 1.5 → X must be numeric.
    /// </summary>
    private static Dictionary<string, Type> CollectFilterTypeHints(XDocument doc, XNamespace ns)
    {
        var hints = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (XElement filter in doc.Descendants(ns + "Filter"))
        {
            string? expression = filter.Element(ns + "FilterExpression")?.Value;
            if (string.IsNullOrWhiteSpace(expression))
                continue;

            MatchCollection matches = FieldsValueRegex.Matches(expression);
            if (matches.Count == 0)
                continue;

            Type? valueType = null;
            foreach (XElement filterValue in filter.Descendants(ns + "FilterValue"))
            {
                Type? inferred = InferTypeFromFilterLiteral(filterValue.Value);
                if (inferred is null)
                    continue;
                valueType = PreferMoreSpecific(valueType ?? typeof(string), inferred);
            }

            if (valueType is null || valueType == typeof(string))
                continue;

            foreach (Match match in matches)
            {
                string fieldName = match.Groups[1].Value;
                if (hints.TryGetValue(fieldName, out Type? existing))
                    hints[fieldName] = PreferMoreSpecific(existing, valueType);
                else
                    hints[fieldName] = valueType;
            }
        }

        return hints;
    }

    private static Type? InferTypeFromFilterLiteral(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string value = raw.Trim();
        if (value.StartsWith('='))
            value = value[1..].Trim();

        // Skip non-literal expressions (Fields!, Parameters!, functions, etc.)
        if (value.Contains('!') || value.Contains('(') || value.Contains('"') || value.Contains('\''))
            return null;

        // Filter literals True/False imply a boolean-compatible column.
        if (bool.TryParse(value, out _))
            return typeof(bool);

        if (int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return typeof(int);

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return typeof(decimal);

        return null;
    }

    /// <summary>BC option/number fields often lack rd:TypeName — infer from common naming.</summary>
    internal static Type InferTypeFromName(string name, string dataField)
    {
        string combined = $"{name} {dataField}".ToLowerInvariant();

        // Flag columns first — names like ShowTotal must NOT become decimal via "total".
        // BC RDLC compares many of these to the strings "True"/"False".
        if (IsBooleanFlagName(name, dataField))
            return typeof(bool);

        if (ContainsAny(combined, "date", "datetime", "time", "periodstart", "periodend"))
            return typeof(DateTime);

        if (ContainsAny(combined, "amount", "price", "cost", "balance", "vat", "discount",
                "qty", "quantity", "rate", "percent", "unitprice", "lcy") ||
            Regex.IsMatch(combined, @"(^|[^a-z])total([^a-z]|$)"))
            return typeof(decimal);

        if (ContainsAny(combined, "lineno", "line_no", "entryno", "entry_no", "document_type",
                "doctype", "type_", "_type", "number", "count", "integer", "option",
                "dimensionsetid", "priority") ||
            Regex.IsMatch(combined, @"(^|[^a-z])type([^a-z]|$)"))
            return typeof(int);

        return typeof(string);
    }

    /// <summary>
    /// BC request-page / buffer flags (ShowGroup, ShowTotal, AsmHeaderExists, …).
    /// </summary>
    internal static bool IsBooleanFlagName(string name, string dataField)
    {
        string n = (name ?? string.Empty).Trim();
        string d = (dataField ?? string.Empty).Trim();
        string combined = $"{n} {d}".ToLowerInvariant();

        if (ContainsAny(combined, "isenabled", "isactive", "boolean", "bool", "asmheaderexists"))
            return true;

        if (ContainsAny(combined, "showgroup", "showtotal", "showinternal", "showcorrection",
                "showlot", "showcust", "showassembly", "printlogo", "loginteraction"))
            return true;

        // Prefixed flags: ShowX / HasX / IsX (PascalCase or underscore).
        foreach (string token in new[] { n, d })
        {
            if (string.IsNullOrEmpty(token))
                continue;
            if (Regex.IsMatch(token, @"^(Show|Has|Is)[A-Z_]", RegexOptions.CultureInvariant))
                return true;
            if (token.EndsWith("Exists", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(haystack.Contains);

    private static Type PreferMoreSpecific(Type current, Type incoming)
    {
        if (current == typeof(string))
            return incoming;
        if (incoming == typeof(string))
            return current;
        if (current == typeof(int) && incoming == typeof(decimal))
            return typeof(decimal);
        if (current == typeof(decimal) && incoming == typeof(int))
            return typeof(decimal);
        return current;
    }

    private static double ParseInches(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        value = value.Trim().ToLowerInvariant().Replace("in", string.Empty).Trim();
        return double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double inches) ? inches : 0;
    }

    private static Type ResolveClrType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return typeof(string);

        string normalized = typeName.Trim();
        int comma = normalized.IndexOf(',');
        if (comma > 0)
            normalized = normalized[..comma].Trim();

        return normalized switch
        {
            "System.Int16" or "Int16" => typeof(short),
            "System.Int32" or "Int32" or "Integer" => typeof(int),
            "System.Int64" or "Int64" or "Long" => typeof(long),
            "System.Byte" or "Byte" => typeof(byte),
            "System.Boolean" or "Boolean" or "Bool" => typeof(bool),
            "System.DateTime" or "DateTime" or "Date" => typeof(DateTime),
            "System.Decimal" or "Decimal" => typeof(decimal),
            "System.Double" or "Double" => typeof(double),
            "System.Single" or "Single" or "Float" => typeof(float),
            "System.Guid" or "Guid" => typeof(Guid),
            "System.Byte[]" or "Byte[]" => typeof(byte[]),
            "System.String" or "String" => typeof(string),
            _ => typeof(string)
        };
    }
}

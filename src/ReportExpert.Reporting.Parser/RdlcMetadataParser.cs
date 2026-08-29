using System.IO;
using System.Xml.Linq;
using ReportExpert.Core.Reporting;
using ReportExpert.Domain.Models;

namespace ReportExpert.Reporting.Parser;

public sealed class RdlcMetadataParser : IRdlcMetadataParser
{
    private static readonly XNamespace Rd = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

    private RdlcReportMetadata? _cached;

    public RdlcReportMetadata? CachedMetadata => _cached;

    public Task<RdlcReportMetadata> ParseAsync(string rdlcPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Parse(rdlcPath), cancellationToken);

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

        var datasets = doc.Descendants(ns + "DataSet")
            .Select(ds => new RdlcDataSetInfo
            {
                Name = ds.Attribute("Name")?.Value ?? "DataSet",
                Fields = ds.Descendants(ns + "Field")
                    .Select(f => new RdlcFieldInfo
                    {
                        Name = f.Attribute("Name")?.Value ?? string.Empty,
                        DataField = f.Element(ns + "DataField")?.Value
                            ?? f.Attribute("Name")?.Value ?? string.Empty,
                        TypeName = f.Element(Rd + "TypeName")?.Value ?? "System.String",
                        ClrType = ResolveClrType(f.Element(Rd + "TypeName")?.Value)
                    })
                    .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                    .ToList()
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

    private static double ParseInches(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        value = value.Trim().ToLowerInvariant().Replace("in", string.Empty).Trim();
        return double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double inches) ? inches : 0;
    }

    private static Type ResolveClrType(string? typeName) => typeName switch
    {
        "System.Int16" => typeof(short),
        "System.Int32" => typeof(int),
        "System.Int64" => typeof(long),
        "System.Byte" => typeof(byte),
        "System.Boolean" => typeof(bool),
        "System.DateTime" => typeof(DateTime),
        "System.Decimal" => typeof(decimal),
        "System.Double" => typeof(double),
        "System.Single" => typeof(float),
        "System.Guid" => typeof(Guid),
        "System.Byte[]" => typeof(byte[]),
        _ => typeof(string)
    };
}

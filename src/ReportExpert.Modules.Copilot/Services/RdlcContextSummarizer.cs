using System.IO;
using System.Text;
using System.Xml.Linq;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>
/// Builds a compact text summary of an RDLC for Copilot context,
/// avoiding huge XML / embedded images that blow token limits.
/// </summary>
public static class RdlcContextSummarizer
{
    private const int MaxSummaryChars = 6_000;
    private const int MaxFieldsPerDataSet = 80;
    private const int MaxReportItems = 60;

    public static string Summarize(string rdlcPath)
    {
        if (string.IsNullOrWhiteSpace(rdlcPath) || !File.Exists(rdlcPath))
            return string.Empty;

        try
        {
            var doc = XDocument.Load(rdlcPath, LoadOptions.None);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            XNamespace rd = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

            var sb = new StringBuilder();
            sb.AppendLine($"File: {Path.GetFileName(rdlcPath)}");

            var pageEl = doc.Descendants(ns + "Page").FirstOrDefault();
            string? width = pageEl?.Element(ns + "PageWidth")?.Value;
            string? height = pageEl?.Element(ns + "PageHeight")?.Value;
            if (!string.IsNullOrWhiteSpace(width) || !string.IsNullOrWhiteSpace(height))
                sb.AppendLine($"Page: {width ?? "?"} × {height ?? "?"}");

            var datasets = doc.Descendants(ns + "DataSet").ToList();
            sb.AppendLine($"Datasets ({datasets.Count}):");
            if (datasets.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                foreach (var ds in datasets)
                {
                    string name = ds.Attribute("Name")?.Value ?? "DataSet";
                    var fields = ds.Descendants(ns + "Field")
                        .Select(f =>
                        {
                            string fieldName = f.Attribute("Name")?.Value ?? string.Empty;
                            string type = f.Element(rd + "TypeName")?.Value
                                          ?? f.Element(ns + "TypeName")?.Value
                                          ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(fieldName))
                                return null;
                            type = ShortType(type);
                            return string.IsNullOrEmpty(type) ? fieldName : $"{fieldName}:{type}";
                        })
                        .Where(x => x is not null)
                        .Cast<string>()
                        .ToList();

                    sb.AppendLine($"  - {name} ({fields.Count} fields)");
                    if (fields.Count > 0)
                    {
                        var shown = fields.Take(MaxFieldsPerDataSet);
                        sb.AppendLine($"    Fields: {string.Join(", ", shown)}");
                        if (fields.Count > MaxFieldsPerDataSet)
                            sb.AppendLine($"    … +{fields.Count - MaxFieldsPerDataSet} more");
                    }
                }
            }

            var parameters = doc.Descendants(ns + "ReportParameter").ToList();
            sb.AppendLine($"Parameters ({parameters.Count}):");
            if (parameters.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                foreach (var p in parameters)
                {
                    string name = p.Attribute("Name")?.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    string dataType = p.Element(ns + "DataType")?.Value ?? "String";
                    sb.AppendLine($"  - {name} ({dataType})");
                }
            }

            var itemNames = doc.Descendants()
                .Where(e => e.Attribute("Name") is not null &&
                            IsReportItem(e.Name.LocalName))
                .Select(e => $"{e.Name.LocalName}:{e.Attribute("Name")!.Value}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxReportItems)
                .ToList();

            sb.AppendLine($"Report items ({itemNames.Count}{(itemNames.Count == MaxReportItems ? "+" : "")}):");
            if (itemNames.Count == 0)
                sb.AppendLine("  (none listed)");
            else
                sb.AppendLine($"  {string.Join(", ", itemNames)}");

            int images = doc.Descendants(ns + "EmbeddedImage").Count();
            if (images > 0)
                sb.AppendLine($"Embedded images: {images} (binary data omitted)");

            string summary = sb.ToString().TrimEnd();
            if (summary.Length > MaxSummaryChars)
                summary = summary[..MaxSummaryChars] + "\n… (summary truncated)";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Could not summarize RDLC: {ex.Message}";
        }
    }

    private static bool IsReportItem(string localName) =>
        localName is "Tablix" or "Textbox" or "TextBox" or "Image" or "Rectangle" or
        "Line" or "Chart" or "Gauge" or "Map" or "Subreport" or "List" or "Table" or
        "Matrix" or "CustomReportItem";

    private static string ShortType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;
        int dot = typeName.LastIndexOf('.');
        return dot >= 0 ? typeName[(dot + 1)..] : typeName;
    }
}

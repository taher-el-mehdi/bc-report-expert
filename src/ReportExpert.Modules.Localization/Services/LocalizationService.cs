using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using ReportExpert.Modules.Localization.Models;

namespace ReportExpert.Modules.Localization.Services;

/// <summary>
/// Reads and writes rd:ReportLabels, manages language layers,
/// and exports labels for translation.
/// </summary>
public sealed class LocalizationService
{
    private static readonly XNamespace Rd = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

    public IReadOnlyList<ReportLabelEntry> LoadLabels(string xml, string language = "default")
    {
        var doc = XDocument.Parse(xml);
        var labelsRoot = doc.Descendants(Rd + "ReportLabels").FirstOrDefault();
        if (labelsRoot is null)
            return [];

        return labelsRoot.Elements(Rd + "ReportLabel")
            .Select(el => new ReportLabelEntry
            {
                LabelName = el.Element(Rd + "LabelName")?.Value ?? string.Empty,
                Value = el.Element(Rd + "Value")?.Value ?? string.Empty,
                Language = el.Attribute("Language")?.Value ?? language
            })
            .Where(l => !string.IsNullOrWhiteSpace(l.LabelName))
            .ToList();
    }

    public IReadOnlyList<string> GetLanguages(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Descendants(Rd + "ReportLabel")
            .Select(el => el.Attribute("Language")?.Value ?? "default")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string ApplyLabels(string xml, IEnumerable<ReportLabelEntry> labels, string language = "default")
    {
        var doc = XDocument.Parse(xml);
        var reportEl = doc.Root ?? throw new InvalidDataException("Missing report root.");

        var labelsRoot = doc.Descendants(Rd + "ReportLabels").FirstOrDefault();
        if (labelsRoot is null)
        {
            labelsRoot = new XElement(Rd + "ReportLabels");
            reportEl.Add(labelsRoot);
        }

        foreach (var existing in labelsRoot.Elements(Rd + "ReportLabel")
                     .Where(e => string.Equals(e.Attribute("Language")?.Value ?? "default", language, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            existing.Remove();
        }

        foreach (var label in labels.Where(l => !string.IsNullOrWhiteSpace(l.LabelName)))
        {
            var el = new XElement(Rd + "ReportLabel",
                new XAttribute("Language", language),
                new XElement(Rd + "LabelName", label.LabelName),
                new XElement(Rd + "Value", label.Value));
            labelsRoot.Add(el);
        }

        return doc.ToString();
    }

    public string ExportForTranslation(IEnumerable<ReportLabelEntry> labels)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LabelName,Language,Value");
        foreach (var label in labels)
        {
            sb.Append(CsvEscape(label.LabelName)).Append(',')
              .Append(CsvEscape(label.Language)).Append(',')
              .AppendLine(CsvEscape(label.Value));
        }

        return sb.ToString();
    }

    public IReadOnlyList<ReportLabelEntry> ImportFromTranslationCsv(string csv)
    {
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
            return [];

        var entries = new List<ReportLabelEntry>();
        foreach (string line in lines.Skip(1))
        {
            string[] parts = ParseCsvLine(line);
            if (parts.Length < 3)
                continue;

            entries.Add(new ReportLabelEntry
            {
                LabelName = parts[0],
                Language = parts[1],
                Value = parts[2]
            });
        }

        return entries;
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"', StringComparison.Ordinal) || value.Contains(',', StringComparison.Ordinal))
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

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
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        parts.Add(current.ToString());
        return parts.ToArray();
    }
}

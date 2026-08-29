using System.Xml.Linq;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>Validates RDLC XML structure and common report-definition issues.</summary>
public static class RdlcXmlValidator
{
    public static (bool IsValid, string Message) Validate(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return (false, "No XML content to validate.");

        try
        {
            var doc = XDocument.Parse(xml);
            var duplicateNames = doc.Descendants()
                .Select(e => (string?)e.Attribute("Name"))
                .Where(n => !string.IsNullOrEmpty(n))
                .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key!)
                .ToList();

            if (duplicateNames.Count > 0)
                return (false, $"Duplicate item names: {string.Join(", ", duplicateNames)}");

            var root = doc.Root;
            if (root is null)
                return (false, "XML has no root element.");

            bool hasBody = root.Descendants().Any(e => e.Name.LocalName == "Body");
            if (!hasBody)
                return (false, "Could not find a Body element in the report.");

            return (true, "XML is well-formed. No duplicate item names detected.");
        }
        catch (Exception ex)
        {
            return (false, $"Invalid XML: {ex.Message}");
        }
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using ReportExpert.Modules.Copilot.Models;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>Extracts structured XML patches from agent responses.</summary>
public static partial class RdlcPatchParser
{
    public static IReadOnlyList<RdlcPatch> Parse(string response)
    {
        var patches = new List<RdlcPatch>();

        foreach (Match match in PatchBlockRegex().Matches(response))
        {
            string body = match.Groups[1].Value.Trim();
            if (TryParseJsonPatch(body, out RdlcPatch? jsonPatch) && jsonPatch is not null)
            {
                patches.Add(jsonPatch);
                continue;
            }

            if (TryParseBeforeAfter(body, out RdlcPatch? baPatch) && baPatch is not null)
                patches.Add(baPatch);
        }

        // Fallback: a single ```xml block in agent mode can be offered as a full-document replace.
        if (patches.Count == 0)
        {
            Match xmlMatch = XmlBlockRegex().Match(response);
            if (xmlMatch.Success)
            {
                string xml = xmlMatch.Groups[1].Value.Trim();
                if (xml.Contains("<Report", StringComparison.OrdinalIgnoreCase))
                {
                    patches.Add(new RdlcPatch
                    {
                        Description = "Replace entire report definition",
                        Replace = xml
                    });
                }
            }
        }

        return patches;
    }

    private static bool TryParseJsonPatch(string body, out RdlcPatch? patch)
    {
        patch = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string replace = root.TryGetProperty("replace", out var replaceEl) ? replaceEl.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(replace))
                return false;

            string? find = root.TryGetProperty("find", out var findEl) ? findEl.GetString() : null;
            string description = root.TryGetProperty("description", out var descEl)
                ? descEl.GetString() ?? "Apply XML patch"
                : "Apply XML patch";

            patch = new RdlcPatch { Description = description, Find = find, Replace = replace };
            return true;
        }
        catch (JsonException)
        {
            // Not a JSON patch body; try the <before>/<after> form instead.
            return false;
        }
    }

    private static bool TryParseBeforeAfter(string body, out RdlcPatch? patch)
    {
        patch = null;
        var beforeMatch = BeforeRegex().Match(body);
        var afterMatch = AfterRegex().Match(body);
        if (!afterMatch.Success)
            return false;

        patch = new RdlcPatch
        {
            Description = "Replace XML fragment",
            Find = beforeMatch.Success ? beforeMatch.Groups[1].Value.Trim() : null,
            Replace = afterMatch.Groups[1].Value.Trim()
        };
        return true;
    }

    public static string ApplyPatch(string currentXml, RdlcPatch patch)
    {
        if (patch.IsFullDocument)
            return patch.Replace;

        if (string.IsNullOrEmpty(patch.Find))
            throw new InvalidOperationException("Patch requires a find fragment for partial replacement.");

        if (!currentXml.Contains(patch.Find, StringComparison.Ordinal))
            throw new InvalidOperationException("Could not locate the XML fragment to replace. Apply manually in the XML tab.");

        return currentXml.Replace(patch.Find, patch.Replace, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"```rdlc-patch\s*\r?\n([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex PatchBlockRegex();

    [GeneratedRegex(@"```xml\s*\r?\n([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex XmlBlockRegex();

    [GeneratedRegex(@"<before>\s*([\s\S]*?)\s*</before>", RegexOptions.IgnoreCase)]
    private static partial Regex BeforeRegex();

    [GeneratedRegex(@"<after>\s*([\s\S]*?)\s*</after>", RegexOptions.IgnoreCase)]
    private static partial Regex AfterRegex();
}

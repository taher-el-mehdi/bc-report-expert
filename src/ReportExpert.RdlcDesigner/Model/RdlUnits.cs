using System.Globalization;
using System.Text.RegularExpressions;

namespace ReportExpert.RdlcDesigner.Model;

/// <summary>
/// RDL size string ↔ device-independent pixels (96 DPI).
/// </summary>
public static class RdlUnits
{
    private static readonly Regex NumberPrefix = new(
        @"^[+-]?\d+(\.\d+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static double ToPx(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('='))
            return 0;

        string v = raw.Trim().ToLowerInvariant();
        Match m = NumberPrefix.Match(v);
        if (!m.Success ||
            !double.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
            return 0;

        if (v.EndsWith("in", StringComparison.Ordinal)) return n * 96;
        if (v.EndsWith("cm", StringComparison.Ordinal)) return n * 37.7952755906;
        if (v.EndsWith("mm", StringComparison.Ordinal)) return n * 3.77952755906;
        if (v.EndsWith("pt", StringComparison.Ordinal)) return n * 96.0 / 72.0;
        if (v.EndsWith("pc", StringComparison.Ordinal)) return n * 16;
        return n;
    }

    /// <summary>
    /// Formats pixels as inches using the same unit family as <paramref name="preferUnitFrom"/> when possible.
    /// </summary>
    public static string FromPx(double px, string? preferUnitFrom = null)
    {
        string unit = DetectUnit(preferUnitFrom) ?? "in";
        double value = unit switch
        {
            "cm" => px / 37.7952755906,
            "mm" => px / 3.77952755906,
            "pt" => px * 72.0 / 96.0,
            "pc" => px / 16.0,
            _ => px / 96.0
        };

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#####}{unit}");
    }

    private static string? DetectUnit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('='))
            return null;

        string v = raw.Trim().ToLowerInvariant();
        if (v.EndsWith("in", StringComparison.Ordinal)) return "in";
        if (v.EndsWith("cm", StringComparison.Ordinal)) return "cm";
        if (v.EndsWith("mm", StringComparison.Ordinal)) return "mm";
        if (v.EndsWith("pt", StringComparison.Ordinal)) return "pt";
        if (v.EndsWith("pc", StringComparison.Ordinal)) return "pc";
        return null;
    }
}

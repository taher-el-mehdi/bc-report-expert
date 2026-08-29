using System.Globalization;
using System.Windows.Media;

namespace ReportExpert.Editors.TextMate;

internal static class TextMateColorHelper
{
    public static Color Parse(string color)
    {
        var normalized = NormalizeColor(color);
        return (Color)ColorConverter.ConvertFromString(normalized)!;
    }

    internal static string NormalizeColor(string color)
    {
        if (color.Length == 9)
        {
            return $"#{color[7]}{color[8]}{color[1]}{color[2]}{color[3]}{color[4]}{color[5]}{color[6]}";
        }

        return color.StartsWith("#", StringComparison.Ordinal) ? color : $"#{color}";
    }
}

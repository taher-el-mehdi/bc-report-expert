using System.Windows.Media;

namespace ReportExpert.RdlcDesigner.Properties;

/// <summary>
/// Font families commonly offered by Report Designer, plus installed system fonts.
/// </summary>
public static class ReportFontCatalog
{
    /// <summary>Core fonts always shown first (VS Report Designer style).</summary>
    private static readonly string[] Preferred =
    [
        "Arial",
        "Arial Black",
        "Arial Narrow",
        "Arial Unicode MS",
        "Calibri",
        "Cambria",
        "Comic Sans MS",
        "Consolas",
        "Courier New",
        "Georgia",
        "Lucida Console",
        "Lucida Sans Unicode",
        "Microsoft Sans Serif",
        "Segoe UI",
        "Tahoma",
        "Times New Roman",
        "Trebuchet MS",
        "Verdana"
    ];

    private static IReadOnlyList<string>? _all;

    /// <summary>Default (empty) + preferred + remaining installed fonts, de-duplicated.</summary>
    public static IReadOnlyList<string> Families
    {
        get
        {
            if (_all is not null)
                return _all;

            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (FontFamily family in Fonts.SystemFontFamilies)
                {
                    string name = family.Source;
                    if (!string.IsNullOrWhiteSpace(name))
                        installed.Add(name);
                }
            }
            catch
            {
                // Design-time / restricted environments may not enumerate fonts.
            }

            var list = new List<string> { "" }; // Default
            foreach (string preferred in Preferred)
            {
                list.Add(preferred);
                installed.Remove(preferred);
            }

            list.AddRange(installed.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            _all = list;
            return _all;
        }
    }

    public static string DisplayName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Default" : value;
}

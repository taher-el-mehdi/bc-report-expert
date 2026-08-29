using System.IO;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Helpers;

public static class LayoutFileHelper
{
    public static readonly string OpenDialogFilter =
        "Report layouts|*.rdlc;*.rdl;*.xlsx;*.docx|RDLC (*.rdlc)|*.rdlc|RDL (*.rdl)|*.rdl|Excel (*.xlsx)|*.xlsx|Word (*.docx)|*.docx|All files (*.*)|*.*";

    public static readonly string CreateLayoutDialogFilter =
        "RDLC layout (*.rdlc)|*.rdlc|RDL layout (*.rdl)|*.rdl|Excel layout (*.xlsx)|*.xlsx|Word layout (*.docx)|*.docx";

    public static bool IsSupportedLayout(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return DetectKind(path) != LayoutKind.None;
    }

    /// <summary>
    /// True for <c>.rdl</c> and <c>.rdlc</c> report definitions. Both share the RDL XML schema.
    /// </summary>
    public static bool IsRdlLayout(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string extension = Path.GetExtension(path);
        return extension.Equals(".rdlc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rdl", StringComparison.OrdinalIgnoreCase);
    }

    public static LayoutKind DetectKind(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".rdlc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rdl", StringComparison.OrdinalIgnoreCase))
            return LayoutKind.Rdlc;
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return LayoutKind.Excel;
        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            return LayoutKind.Word;
        return LayoutKind.None;
    }

    /// <summary>Short label for the explorer and editor chrome: RDLC, RDL, Excel, or Word.</summary>
    public static string FormatLabel(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Equals(".rdl", StringComparison.OrdinalIgnoreCase))
            return "RDL";
        if (extension.Equals(".rdlc", StringComparison.OrdinalIgnoreCase))
            return "RDLC";
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return "Excel";
        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            return "Word";
        return string.Empty;
    }

    /// <summary>Lower is better. Prefer RDLC/RDL, then Excel, then Word.</summary>
    public static int MatchRank(string path) => DetectKind(path) switch
    {
        LayoutKind.Rdlc => 0,
        LayoutKind.Excel => 1,
        LayoutKind.Word => 2,
        _ => 9
    };
}

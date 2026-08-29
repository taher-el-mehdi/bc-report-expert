namespace ReportExpert.Domain.Models;

public enum WorkspaceFileKind
{
    Layout,
    Report
}

public sealed class WorkspaceFileEntry
{
    public required string FullPath { get; init; }
    public required string RelativePath { get; init; }
    public required string Name { get; init; }
    public WorkspaceFileKind Kind { get; init; }

    public string DisplayPath => RelativePath.Replace('\\', '/');

    /// <summary>Short title without extension (and without .Report for AL reports).</summary>
    public string DisplayTitle
    {
        get
        {
            if (Kind == WorkspaceFileKind.Report)
            {
                string name = Name;
                if (name.EndsWith(".Report.al", StringComparison.OrdinalIgnoreCase))
                    return name[..^".Report.al".Length];
                if (name.EndsWith(".al", StringComparison.OrdinalIgnoreCase))
                    return name[..^".al".Length];
            }

            return System.IO.Path.GetFileNameWithoutExtension(Name);
        }
    }

    /// <summary>RDLC, RDL, Excel, Word, or empty for non-layout entries.</summary>
    public string LayoutFormat
    {
        get
        {
            string ext = System.IO.Path.GetExtension(Name);
            if (ext.Equals(".rdl", StringComparison.OrdinalIgnoreCase))
                return "RDL";
            if (ext.Equals(".rdlc", StringComparison.OrdinalIgnoreCase))
                return "RDLC";
            if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return "Excel";
            if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                return "Word";
            return string.Empty;
        }
    }
}

/// <summary>
/// A project explorer node for a layout file (.rdlc, .rdl, .xlsx, .docx).
/// </summary>
public sealed class WorkspaceItemNode
{
    public required WorkspaceFileEntry File { get; init; }
    public IReadOnlyList<WorkspaceFileEntry> Layouts { get; init; } = [];

    public bool HasLayouts => Layouts.Count > 0;
    public bool IsReport => File.Kind == WorkspaceFileKind.Report;
    public bool IsStandaloneLayout => File.Kind == WorkspaceFileKind.Layout;

    public string DisplayTitle => File.DisplayTitle;

    public string Summary
    {
        get
        {
            string format = File.LayoutFormat;
            return string.IsNullOrEmpty(format)
                ? File.DisplayPath
                : $"{format} · {File.DisplayPath}";
        }
    }

    public bool MatchesSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return Contains(File.Name, query) ||
               Contains(File.DisplayTitle, query) ||
               Contains(File.RelativePath, query) ||
               Contains(File.LayoutFormat, query);
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) &&
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

public sealed class RecentProjectEntry
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime LastOpenedUtc { get; set; }
}

public sealed class WorkspaceSnapshot
{
    public required string RootPath { get; init; }
    public required string Name { get; init; }

    /// <summary>Flat list of layout files in the project.</summary>
    public IReadOnlyList<WorkspaceItemNode> Items { get; init; } = [];

    public IReadOnlyList<WorkspaceFileEntry> Layouts { get; init; } = [];
    public IReadOnlyList<WorkspaceFileEntry> Reports { get; init; } = [];

    public int LayoutCount => Layouts.Count;
    public int ReportCount => Reports.Count;
    public int TotalFileCount => LayoutCount + ReportCount;
}

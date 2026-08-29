using System.Collections.ObjectModel;
using System.Data;

namespace ReportExpert.Modules.Preview.Models;

/// <summary>In-app preview model for Excel (.xlsx) or Word (.docx) report layouts.</summary>
public sealed class OfficeLayoutPreview
{
    public required string FilePath { get; init; }
    public required LayoutKind Kind { get; init; }
    public required string FileName { get; init; }
    public required string FileSizeDisplay { get; init; }
    public string Summary { get; set; } = string.Empty;

    public IReadOnlyList<string> SheetNames { get; init; } = [];
    public string? SelectedSheetName { get; set; }
    public DataTable? SheetTable { get; set; }
    public string WordPreviewText { get; set; } = string.Empty;

    public string KindDisplay => Kind switch
    {
        LayoutKind.Excel => "Excel",
        LayoutKind.Word => "Word",
        _ => "Layout"
    };

    public bool IsExcel => Kind == LayoutKind.Excel;
    public bool IsWord => Kind == LayoutKind.Word;
}

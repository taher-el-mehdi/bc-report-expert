using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.Modules.Preview.Services;

/// <summary>
/// Builds a lightweight in-app preview for Business Central Excel/Word layout files
/// using the Open XML SDK (no Office installation required).
/// </summary>
public sealed class OfficeLayoutPreviewService
{
    private const int MaxPreviewRows = 200;
    private const int MaxPreviewColumns = 40;
    private const int MaxWordChars = 100_000;

    public Task<OfficeLayoutPreview> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(path), cancellationToken);

    public OfficeLayoutPreview Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Layout file not found.", path);

        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath);
        var kind = extension.ToLowerInvariant() switch
        {
            ".xlsx" => LayoutKind.Excel,
            ".docx" => LayoutKind.Word,
            _ => throw new NotSupportedException($"Unsupported layout format: {extension}")
        };

        return kind == LayoutKind.Excel
            ? LoadExcel(fullPath)
            : LoadWord(fullPath);
    }

    private static OfficeLayoutPreview LoadExcel(string path)
    {
        using var doc = SpreadsheetDocument.Open(path, false);
        WorkbookPart workbookPart = doc.WorkbookPart
            ?? throw new InvalidDataException("Excel layout has no workbook part.");

        SharedStringTable? sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var sheetNames = workbookPart.Workbook.Sheets?
            .Elements<Sheet>()
            .Select(s => s.Name?.Value)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToList() ?? [];

        string? selected = sheetNames.FirstOrDefault();
        DataTable? table = selected is null
            ? null
            : ReadSheetTable(workbookPart, sharedStrings, selected);

        return new OfficeLayoutPreview
        {
            FilePath = path,
            Kind = LayoutKind.Excel,
            FileName = Path.GetFileName(path),
            FileSizeDisplay = FormatFileSize(path),
            SheetNames = sheetNames,
            SelectedSheetName = selected,
            SheetTable = table,
            Summary = sheetNames.Count switch
            {
                0 => "No worksheets found.",
                1 => "1 worksheet",
                _ => $"{sheetNames.Count} worksheets"
            }
        };
    }

    public DataTable? LoadExcelSheet(string path, string sheetName)
    {
        using var doc = SpreadsheetDocument.Open(path, false);
        WorkbookPart workbookPart = doc.WorkbookPart
            ?? throw new InvalidDataException("Excel layout has no workbook part.");
        SharedStringTable? sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        return ReadSheetTable(workbookPart, sharedStrings, sheetName);
    }

    private static DataTable ReadSheetTable(
        WorkbookPart workbookPart,
        SharedStringTable? sharedStrings,
        string sheetName)
    {
        Sheet? sheet = workbookPart.Workbook.Sheets?
            .Elements<Sheet>()
            .FirstOrDefault(s => string.Equals(s.Name?.Value, sheetName, StringComparison.OrdinalIgnoreCase));

        if (sheet?.Id?.Value is null)
            return new DataTable(sheetName);

        if (workbookPart.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
            return new DataTable(sheetName);

        SheetData? sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
        var table = new DataTable(sheetName);

        if (sheetData is null)
            return table;

        var rows = sheetData.Elements<Row>().Take(MaxPreviewRows + 1).ToList();
        if (rows.Count == 0)
            return table;

        int columnCount = rows
            .Select(r => r.Elements<Cell>().Select(GetColumnIndex).DefaultIfEmpty(0).Max() + 1)
            .DefaultIfEmpty(0)
            .Max();
        columnCount = Math.Clamp(columnCount, 1, MaxPreviewColumns);

        for (int i = 0; i < columnCount; i++)
            table.Columns.Add(GetExcelColumnName(i), typeof(string));

        foreach (Row row in rows.Take(MaxPreviewRows))
        {
            DataRow dataRow = table.NewRow();
            foreach (Cell cell in row.Elements<Cell>())
            {
                int col = GetColumnIndex(cell);
                if (col < 0 || col >= columnCount)
                    continue;

                dataRow[col] = GetCellText(cell, sharedStrings);
            }

            table.Rows.Add(dataRow);
        }

        if (rows.Count > MaxPreviewRows)
        {
            DataRow note = table.NewRow();
            note[0] = $"[Preview truncated to {MaxPreviewRows} rows]";
            table.Rows.Add(note);
        }

        return table;
    }

    private static OfficeLayoutPreview LoadWord(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        Body? body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return new OfficeLayoutPreview
            {
                FilePath = path,
                Kind = LayoutKind.Word,
                FileName = Path.GetFileName(path),
                FileSizeDisplay = FormatFileSize(path),
                WordPreviewText = "(Empty document)",
                Summary = "Empty Word layout"
            };
        }

        var paragraphs = body.Elements<Paragraph>().ToList();
        var sb = new StringBuilder();
        int charCount = 0;

        foreach (Paragraph paragraph in paragraphs)
        {
            string text = paragraph.InnerText;
            if (charCount + text.Length + 2 > MaxWordChars)
            {
                sb.AppendLine();
                sb.AppendLine($"[Preview truncated — showing first {MaxWordChars:N0} characters]");
                break;
            }

            sb.AppendLine(text);
            charCount += text.Length + 2;
        }

        string previewText = sb.ToString().TrimEnd();
        if (string.IsNullOrWhiteSpace(previewText))
            previewText = "(No visible text in this Word layout — it may be template XML / content controls only.)";

        return new OfficeLayoutPreview
        {
            FilePath = path,
            Kind = LayoutKind.Word,
            FileName = Path.GetFileName(path),
            FileSizeDisplay = FormatFileSize(path),
            WordPreviewText = previewText,
            Summary = $"{paragraphs.Count} paragraph(s)"
        };
    }

    private static string GetCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        string raw = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
            sharedStrings is not null &&
            index >= 0 &&
            index < sharedStrings.ChildElements.Count)
        {
            return sharedStrings.ElementAt(index).InnerText;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? raw;

        return raw;
    }

    private static int GetColumnIndex(Cell cell)
    {
        string? reference = cell.CellReference?.Value;
        if (string.IsNullOrWhiteSpace(reference))
            return 0;

        int index = 0;
        foreach (char ch in reference)
        {
            if (!char.IsLetter(ch))
                break;

            index = (index * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return Math.Max(0, index - 1);
    }

    private static string GetExcelColumnName(int zeroBasedIndex)
    {
        int n = zeroBasedIndex + 1;
        var sb = new StringBuilder();
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + (n % 26)));
            n /= 26;
        }

        return sb.ToString();
    }

    private static string FormatFileSize(string path)
    {
        try
        {
            long bytes = new FileInfo(path).Length;
            return bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
                _ => $"{bytes / (1024.0 * 1024.0):0.##} MB"
            };
        }
        catch
        {
            return string.Empty;
        }
    }
}

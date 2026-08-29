using System.Diagnostics;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ReportExpert.LayoutSmoke;

internal static class ExcelProbe
{
    public static int Run(string path)
    {
        Console.WriteLine($"Opening: {path}");
        var sw = Stopwatch.StartNew();
        using var doc = SpreadsheetDocument.Open(path, isEditable: false);
        Console.WriteLine($"Opened in {sw.ElapsedMilliseconds}ms");

        WorkbookPart wb = doc.WorkbookPart ?? throw new InvalidDataException("no workbook");
        var sheets = wb.Workbook.Sheets?.Elements<Sheet>().ToList() ?? [];
        Console.WriteLine($"Sheets: {sheets.Count}");

        foreach (Sheet sheet in sheets)
        {
            Console.WriteLine($"  - {sheet.Name}");
            if (sheet.Id?.Value is null)
                continue;

            var part = (WorksheetPart)wb.GetPartById(sheet.Id.Value);
            var data = part.Worksheet.GetFirstChild<SheetData>();
            int rows = data?.ChildElements.Count ?? 0;
            Console.WriteLine($"    childElements={rows} elapsed={sw.ElapsedMilliseconds}ms");
        }

        Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms");
        return 0;
    }
}

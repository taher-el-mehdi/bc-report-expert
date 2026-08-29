using System.IO;
using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Models;
using ReportExpert.Modules.Preview.Services;

namespace ReportExpert.LayoutSmoke;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0].Equals("--excel-only", StringComparison.OrdinalIgnoreCase))
            return ExcelProbe.Run(args[1]);

        if (args.Length >= 1 && args[0].Equals("--ui", StringComparison.OrdinalIgnoreCase))
        {
            string folder = args.Length >= 2
                ? args[1]
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "test-reports"));
            return UiSmoke.Run(folder);
        }

        if (args.Length >= 1 && args[0].Equals("--home", StringComparison.OrdinalIgnoreCase))
        {
            string folder = args.Length >= 2
                ? args[1]
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "test-reports"));
            return HomeSmoke.Run(folder);
        }

        return RunParserSmokeAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> RunParserSmokeAsync(string[] args)
    {
        string root = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "test-reports"));

        if (!Directory.Exists(root))
            root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "test-reports"));

        Console.WriteLine($"Test folder: {root}");
        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine("test-reports folder not found.");
            return 1;
        }

        var office = new OfficeLayoutPreviewService();
        var parser = new RdlcMetadataParser();
        int failures = 0;

        foreach (string path in Directory.EnumerateFiles(root)
                     .OrderBy(LayoutFileHelper.MatchRank)
                     .ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(path);
            var kind = LayoutFileHelper.DetectKind(path);
            Console.WriteLine();
            Console.WriteLine($"== {name} ({kind}) ==");
            Console.Out.Flush();

            try
            {
                switch (kind)
                {
                    case LayoutKind.Rdlc:
                    {
                        var meta = await parser.ParseAsync(path);
                        Console.WriteLine(
                            $"  Parse OK: {meta.ReportName}, datasets={meta.DataSets.Count}, params={meta.Parameters.Count}");
                        break;
                    }
                    case LayoutKind.Excel:
                    case LayoutKind.Word:
                    {
                        Console.WriteLine("  Loading Office layout...");
                        Console.Out.Flush();
                        var preview = office.Load(path);
                        Console.WriteLine(
                            $"  Office OK: {preview.KindDisplay}, {preview.Summary}, sheets={preview.SheetNames.Count}, textChars={preview.WordPreviewText.Length}");
                        if (preview.SheetTable is not null)
                        {
                            Console.WriteLine(
                                $"  Sheet '{preview.SelectedSheetName}': {preview.SheetTable.Rows.Count} rows x {preview.SheetTable.Columns.Count} cols");
                        }

                        break;
                    }
                    default:
                        Console.WriteLine("  SKIP unsupported");
                        break;
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException is not null)
                    Console.WriteLine($"       Inner: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL PASSED" : $"FAILURES: {failures}");
        return failures == 0 ? 0 : 2;
    }
}

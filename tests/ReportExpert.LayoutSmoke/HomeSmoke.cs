using System.IO;
using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Models;
using ReportExpert.Modules.Preview.Services;
using unvell.ReoGrid;
using unvell.ReoGrid.IO;

namespace ReportExpert.LayoutSmoke;

/// <summary>
/// Exercises Home-side open paths: RDLC metadata, ReoGrid Excel, and
/// <see cref="DocxFlowDocumentService"/> Word preview (same path as Home UI).
/// </summary>
internal static class HomeSmoke
{
    public static int Run(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"Folder not found: {folder}");
            return 1;
        }

        var parser = new RdlcMetadataParser();
        var wordPreview = new DocxFlowDocumentService();
        int failures = 0;

        foreach (string path in Directory.EnumerateFiles(folder)
                     .Where(LayoutFileHelper.IsSupportedLayout)
                     .OrderBy(LayoutFileHelper.MatchRank)
                     .ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(path);
            var kind = LayoutFileHelper.DetectKind(path);
            Console.WriteLine($"HOME: {name} ({kind})");
            Console.Out.Flush();

            try
            {
                switch (kind)
                {
                    case LayoutKind.Rdlc:
                    {
                        var meta = parser.Parse(path);
                        Console.WriteLine(
                            $"  Structure OK: datasets={meta.DataSets.Count}, params={meta.Parameters.Count}, page={meta.PageSizeDisplay}");
                        break;
                    }
                    case LayoutKind.Excel:
                    {
                        Console.WriteLine("  ReoGrid load...");
                        Console.Out.Flush();
                        var grid = new ReoGridControl();
                        grid.Load(path, FileFormat.Excel2007);
                        Console.WriteLine($"  ReoGrid OK: sheets={grid.Worksheets.Count}");
                        break;
                    }
                    case LayoutKind.Word:
                    {
                        Console.WriteLine("  DocxFlowDocument load...");
                        Console.Out.Flush();
                        (_, string summary) = wordPreview.LoadWithSummary(path);
                        Console.WriteLine($"  Word FlowDocument OK: {summary}");
                        break;
                    }
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

        Console.WriteLine(failures == 0 ? "HOME SMOKE PASSED" : $"HOME SMOKE FAILURES: {failures}");
        return failures == 0 ? 0 : 4;
    }
}

using System.IO;
using System.Reflection;
using ReportExpert.Common;

namespace ReportExpert.Editors.Services;

/// <summary>Extracts embedded TextMate grammar resources to a stable cache directory.</summary>
internal static class AlGrammarResourceCache
{
    private static readonly object Sync = new();
    private static string? _grammarDirectory;

    public const string AlScopeName = "source.al";

    public static string EnsureGrammarDirectory()
    {
        if (_grammarDirectory != null)
        {
            return _grammarDirectory;
        }

        lock (Sync)
        {
            if (_grammarDirectory != null)
            {
                return _grammarDirectory;
            }

            string target = AppConstants.GetTempDirectory(AppConstants.TempGrammarsFolderName, "al");

            ExtractResource("ReportExpert.Editors.Resources.Syntax.al.tmLanguage.json", Path.Combine(target, "al.tmLanguage.json"));
            ExtractResource("ReportExpert.Editors.Resources.Syntax.al.package.json", Path.Combine(target, "al.package.json"));

            _grammarDirectory = target;
            return target;
        }
    }

    private static void ExtractResource(string resourceName, string targetPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded grammar resource '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);
        string content = reader.ReadToEnd();

        if (File.Exists(targetPath))
        {
            string existing = File.ReadAllText(targetPath);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return;
            }
        }

        File.WriteAllText(targetPath, content);
    }
}

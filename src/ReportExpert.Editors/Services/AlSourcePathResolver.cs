using System.IO;
using ReportExpert.Core.Editors;

namespace ReportExpert.Editors.Services;

public sealed class AlSourcePathResolver : IAlSourcePathResolver
{
    /// <summary>Optional project root used to locate AL reports under Layouts/Reports folders.</summary>
    public string? WorkspaceRoot { get; set; }

    public string? ResolveFromRdlc(string rdlcPath)
    {
        if (string.IsNullOrWhiteSpace(rdlcPath) || !File.Exists(rdlcPath))
        {
            return null;
        }

        string fullRdlcPath = Path.GetFullPath(rdlcPath);

        string? sibling = ResolveSiblingAl(fullRdlcPath);
        if (sibling is not null)
            return sibling;

        return ResolveFromWorkspace(fullRdlcPath);
    }

    private string? ResolveFromWorkspace(string rdlcFullPath)
    {
        if (string.IsNullOrWhiteSpace(WorkspaceRoot) || !Directory.Exists(WorkspaceRoot))
            return null;

        string baseName = Path.GetFileNameWithoutExtension(rdlcFullPath);
        string root = Path.GetFullPath(WorkspaceRoot);

        // Prefer *.Report.al named after the layout.
        string expectedName = $"{baseName}.Report.al";
        try
        {
            foreach (string candidate in Directory.EnumerateFiles(root, "*.Report.al", SearchOption.AllDirectories))
            {
                if (IsExcludedPath(root, candidate))
                    continue;

                if (Path.GetFileName(candidate).Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            foreach (string candidate in Directory.EnumerateFiles(root, "*.Report.al", SearchOption.AllDirectories))
            {
                if (IsExcludedPath(root, candidate))
                    continue;

                string candidateBase = Path.GetFileNameWithoutExtension(candidate);
                if (candidateBase.Equals($"{baseName}.Report", StringComparison.OrdinalIgnoreCase) ||
                    candidateBase.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool IsExcludedPath(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string part in parts)
        {
            if (part.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                part.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                part.Equals(".alpackages", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveSiblingAl(string rdlcFullPath)
    {
        string? directory = Path.GetDirectoryName(rdlcFullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        string baseName = Path.GetFileNameWithoutExtension(rdlcFullPath);

        string reportAl = Path.Combine(directory, $"{baseName}.Report.al");
        if (File.Exists(reportAl))
        {
            return reportAl;
        }

        // Common AL layout: Reports\Foo.Report.al + Layouts\Foo.rdlc (sibling folders).
        string? parent = Directory.GetParent(directory)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            string[] reportDirs =
            [
                Path.Combine(parent, "Reports"),
                Path.Combine(parent, "Report"),
                Path.Combine(parent, "src", "Reports"),
                Path.Combine(parent, "src", "Report")
            ];

            foreach (string reportDir in reportDirs)
            {
                if (!Directory.Exists(reportDir))
                    continue;

                string candidate = Path.Combine(reportDir, $"{baseName}.Report.al");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        foreach (string candidate in Directory.EnumerateFiles(directory, "*.Report.al"))
        {
            string candidateBase = Path.GetFileNameWithoutExtension(candidate);
            if (candidateBase.Equals($"{baseName}.Report", StringComparison.OrdinalIgnoreCase) ||
                candidateBase.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }
}

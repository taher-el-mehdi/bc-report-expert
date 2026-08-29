using System.IO;
using System.Text.Json;
using ReportExpert.Common;
using ReportExpert.Core.Workspace;
using ReportExpert.Domain.Models;
using ReportExpert.Modules.Preview.Helpers;

namespace ReportExpert.App.Workspace;

/// <summary>
/// Scans a project folder for RDL / RDLC / Office / AL reports and persists recent project paths.
/// </summary>
public sealed class WorkspaceService : IWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".alpackages", ".snapshots",
        "bin", "obj", "node_modules", "packages", ".idea",
        ".report_expert"
    };

    private readonly string _recentPath;
    private List<RecentProjectEntry> _recentProjects = [];
    private WorkspaceSnapshot? _current;

    public WorkspaceSnapshot? Current => _current;

    public IReadOnlyList<RecentProjectEntry> RecentProjects => _recentProjects;

    public event Action<WorkspaceSnapshot?>? WorkspaceChanged;

    public WorkspaceService()
    {
        _recentPath = AppConstants.GetAppDataPath(AppConstants.RecentProjectsFileName);
        LoadRecent();
    }

    public WorkspaceSnapshot? OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return null;

        string root = Path.GetFullPath(folderPath);
        _current = Scan(root);
        Remember(_current);
        WorkspaceChanged?.Invoke(_current);
        return _current;
    }

    public void Close()
    {
        if (_current is null)
            return;

        _current = null;
        WorkspaceChanged?.Invoke(null);
    }

    public void Refresh()
    {
        if (_current is null)
            return;

        _current = Scan(_current.RootPath);
        Remember(_current);
        WorkspaceChanged?.Invoke(_current);
    }

    private static WorkspaceSnapshot Scan(string root)
    {
        var layouts = new List<WorkspaceFileEntry>();

        foreach (string file in EnumerateProjectFiles(root))
        {
            string relative = Path.GetRelativePath(root, file);
            string name = Path.GetFileName(file);

            layouts.Add(new WorkspaceFileEntry
            {
                FullPath = file,
                RelativePath = relative,
                Name = name,
                Kind = WorkspaceFileKind.Layout
            });
        }

        layouts.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        var items = layouts
            .Select(layout => new WorkspaceItemNode
            {
                File = layout,
                Layouts = []
            })
            .ToList();

        return new WorkspaceSnapshot
        {
            RootPath = root,
            Name = new DirectoryInfo(root).Name,
            Items = items,
            Layouts = layouts,
            Reports = []
        };
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string dir = pending.Pop();
            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (string subdir in subdirs)
            {
                string name = Path.GetFileName(subdir);
                if (ExcludedDirectoryNames.Contains(name))
                    continue;

                pending.Push(subdir);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (string file in files)
            {
                if (LayoutFileHelper.IsSupportedLayout(file))
                    yield return file;
            }
        }
    }

    private void Remember(WorkspaceSnapshot workspace)
    {
        _recentProjects.RemoveAll(p =>
            string.Equals(p.Path, workspace.RootPath, StringComparison.OrdinalIgnoreCase));

        _recentProjects.Insert(0, new RecentProjectEntry
        {
            Path = workspace.RootPath,
            Name = workspace.Name,
            LastOpenedUtc = DateTime.UtcNow
        });

        if (_recentProjects.Count > 10)
            _recentProjects = _recentProjects.Take(10).ToList();

        SaveRecent();
    }

    private void LoadRecent()
    {
        if (!File.Exists(_recentPath))
        {
            _recentProjects = [];
            return;
        }

        try
        {
            string json = File.ReadAllText(_recentPath);
            _recentProjects = JsonSerializer.Deserialize<List<RecentProjectEntry>>(json) ?? [];
            _recentProjects = _recentProjects
                .Where(p => !string.IsNullOrWhiteSpace(p.Path) && Directory.Exists(p.Path))
                .ToList();
        }
        catch
        {
            _recentProjects = [];
        }
    }

    private void SaveRecent()
    {
        string json = JsonSerializer.Serialize(_recentProjects, JsonOptions);
        File.WriteAllText(_recentPath, json);
    }
}

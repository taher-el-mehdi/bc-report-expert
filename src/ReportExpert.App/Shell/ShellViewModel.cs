using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportExpert.App.Home;
using ReportExpert.App.Workspace;
using ReportExpert.Domain.Models;
using ReportExpert.Modules.Preview.Helpers;

namespace ReportExpert.App.Shell;

/// <summary>
/// Shell state for the main window: workspace open/close, recent projects, explorer tree, and Home viewer.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly WorkspaceService _workspaceService;
    private readonly Func<Window?> _getOwner;
    private readonly List<WorkspaceItemNode> _allItems = [];

    public HomeWorkspaceViewModel HomeViewer { get; } = new();

    [ObservableProperty]
    private bool _hasWorkspace;

    [ObservableProperty]
    private string _workspaceName = string.Empty;

    [ObservableProperty]
    private string _workspaceRootPath = string.Empty;

    [ObservableProperty]
    private string _workspaceSummary = string.Empty;

    [ObservableProperty]
    private string _windowTitle = "Report Expert";

    [ObservableProperty]
    private WorkspaceFileEntry? _selectedFile;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<WorkspaceItemNode> FilteredItems { get; } = [];
    public ObservableCollection<RecentProjectEntry> RecentProjects { get; } = [];

    public event Action<WorkspaceFileEntry>? FileOpenRequested;
    public event Action<WorkspaceSnapshot?>? WorkspaceChanged;

    public ShellViewModel(WorkspaceService workspaceService, Func<Window?> getOwner)
    {
        _workspaceService = workspaceService;
        _getOwner = getOwner;
        _workspaceService.WorkspaceChanged += OnWorkspaceChanged;
        RefreshRecentProjects();
    }

    partial void OnSearchTextChanged(string value) => ApplySearchFilter();

    [RelayCommand]
    private void OpenProject()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Open AL extension folder",
            Multiselect = false
        };

        Window? owner = _getOwner();
        bool? result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return;

        _workspaceService.OpenFolder(dialog.FolderName);
    }

    [RelayCommand]
    private void OpenRecentProject(RecentProjectEntry? entry)
    {
        if (entry is null || !Directory.Exists(entry.Path))
            return;

        _workspaceService.OpenFolder(entry.Path);
    }

    [RelayCommand]
    private void RefreshWorkspace()
    {
        if (!HasWorkspace)
            return;

        _workspaceService.Refresh();
    }

    [RelayCommand]
    private void OpenWorkspaceFile(WorkspaceFileEntry? entry)
    {
        if (entry is null || !File.Exists(entry.FullPath))
            return;

        SelectedFile = entry;
        FileOpenRequested?.Invoke(entry);
    }

    [RelayCommand]
    private void CreateNewReport()
    {
        if (!HasWorkspace || string.IsNullOrWhiteSpace(WorkspaceRootPath))
            return;

        string reportsDir = PreferSubfolder(WorkspaceRootPath, "src", "Reports");
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Create new AL report",
            InitialDirectory = reportsDir,
            FileName = "NewReport.Report.al",
            Filter = "AL Report (*.Report.al)|*.Report.al|AL files (*.al)|*.al",
            DefaultExt = ".Report.al",
            AddExtension = true
        };

        Window? owner = _getOwner();
        if (owner is null ? dialog.ShowDialog() != true : dialog.ShowDialog(owner) != true)
            return;

        try
        {
            string objectName = Path.GetFileName(dialog.FileName);
            if (objectName.EndsWith(".Report.al", StringComparison.OrdinalIgnoreCase))
                objectName = objectName[..^".Report.al".Length];
            else if (objectName.EndsWith(".al", StringComparison.OrdinalIgnoreCase))
                objectName = objectName[..^".al".Length];

            string path = WorkspaceFileFactory.CreateReport(dialog.FileName, objectName);
            _workspaceService.Refresh();
            OpenCreatedFile(path, WorkspaceFileKind.Report);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not create report:\n\n{ex.Message}",
                "Report Expert",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CreateNewLayout()
    {
        if (!HasWorkspace || string.IsNullOrWhiteSpace(WorkspaceRootPath))
            return;

        string layoutsDir = PreferSubfolder(WorkspaceRootPath, "src", "Layouts");
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Create new layout",
            InitialDirectory = layoutsDir,
            FileName = "NewLayout.rdlc",
            Filter = LayoutFileHelper.CreateLayoutDialogFilter,
            DefaultExt = ".rdlc",
            AddExtension = true
        };

        Window? owner = _getOwner();
        if (owner is null ? dialog.ShowDialog() != true : dialog.ShowDialog(owner) != true)
            return;

        try
        {
            string path = WorkspaceFileFactory.CreateLayout(dialog.FileName);
            _workspaceService.Refresh();
            OpenCreatedFile(path, WorkspaceFileKind.Layout);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not create layout:\n\n{ex.Message}",
                "Report Expert",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenCreatedFile(string path, WorkspaceFileKind kind)
    {
        var entry = new WorkspaceFileEntry
        {
            FullPath = path,
            RelativePath = Path.GetRelativePath(WorkspaceRootPath, path),
            Name = Path.GetFileName(path),
            Kind = kind
        };
        OpenWorkspaceFile(entry);
    }

    private static string PreferSubfolder(string root, params string[] parts)
    {
        string candidate = Path.Combine(new[] { root }.Concat(parts).ToArray());
        Directory.CreateDirectory(candidate);
        return candidate;
    }

    private void OnWorkspaceChanged(WorkspaceSnapshot? snapshot) =>
        System.Windows.Application.Current.Dispatcher.Invoke(() => ApplyWorkspace(snapshot));

    private void ApplyWorkspace(WorkspaceSnapshot? snapshot)
    {
        _allItems.Clear();
        FilteredItems.Clear();

        if (snapshot is null)
        {
            SearchText = string.Empty;
            HasWorkspace = false;
            WorkspaceName = string.Empty;
            WorkspaceRootPath = string.Empty;
            WorkspaceSummary = string.Empty;
            WindowTitle = "Report Expert";
            SelectedFile = null;
            HomeViewer.Reset();
            RefreshRecentProjects();
            WorkspaceChanged?.Invoke(null);
            return;
        }

        bool projectChanged = !string.Equals(WorkspaceRootPath, snapshot.RootPath, StringComparison.OrdinalIgnoreCase);
        HasWorkspace = true;
        WorkspaceName = snapshot.Name;
        WorkspaceRootPath = snapshot.RootPath;
        WorkspaceSummary = snapshot.LayoutCount == 1
            ? "1 layout"
            : $"{snapshot.LayoutCount} layouts";
        WindowTitle = $"Report Expert — {snapshot.Name}";
        HomeViewer.Reset();

        foreach (var item in snapshot.Items)
            _allItems.Add(item);

        if (projectChanged)
            SearchText = string.Empty;

        ApplySearchFilter();
        RefreshRecentProjects();
        WorkspaceChanged?.Invoke(snapshot);
    }

    private void ApplySearchFilter()
    {
        FilteredItems.Clear();
        string query = SearchText;

        foreach (var item in _allItems)
        {
            if (item.MatchesSearch(query))
                FilteredItems.Add(item);
        }
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var project in _workspaceService.RecentProjects)
            RecentProjects.Add(project);
    }
}

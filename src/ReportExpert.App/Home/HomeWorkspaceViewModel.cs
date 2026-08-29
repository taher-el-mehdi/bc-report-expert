using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportExpert.Domain.Models;
using ReportExpert.Editors.Editors;
using ReportExpert.Editors.Services;
using ReportExpert.Editors.ViewModels;
using ReportExpert.Modules.Preview.Helpers;
using ReportExpert.Modules.Preview.Models;

namespace ReportExpert.App.Home;

public enum HomeContentKind
{
    Idle,
    Layout,
    AlSource
}

/// <summary>
/// Home after workspace open: Excel ReoGrid editor, Word Open XML preview,
/// RDLC structure + visual layout, AL source viewer.
/// </summary>
public sealed partial class HomeWorkspaceViewModel : ObservableObject
{
    private CancellationTokenSource? _loadCts;

    public AlSourceViewerViewModel AlSource { get; }

    [ObservableProperty]
    private HomeContentKind _contentKind = HomeContentKind.Idle;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAgentEditing;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isCopilotOpen;

    [ObservableProperty]
    private string _statusMessage = "Select a layout or AL report from the explorer.";

    [ObservableProperty]
    private string _fileTitle = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _formatDisplay = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private LayoutKind _layoutKind = LayoutKind.None;

    public bool IsIdle => ContentKind == HomeContentKind.Idle;
    public bool IsShowingLayout => ContentKind == HomeContentKind.Layout;
    public bool IsShowingAl => ContentKind == HomeContentKind.AlSource;
    public bool IsRdlcLayout => LayoutKind == LayoutKind.Rdlc;
    public bool IsExcelLayout => LayoutKind == LayoutKind.Excel;
    public bool IsWordLayout => LayoutKind == LayoutKind.Word;

    /// <summary>Excel and RDLC support in-app Save. Word is preview-only (edit in Microsoft Word).</summary>
    public bool CanSave => (IsExcelLayout || IsRdlcLayout) && !string.IsNullOrWhiteSpace(FilePath);

    public bool CanUseCopilot => IsRdlcLayout;

    public event Action<string>? OpenLayoutInPreviewRequested;
    public event Action? LayoutEditorLoadRequested;
    public event Action? LayoutEditorSaveRequested;

    public HomeWorkspaceViewModel()
    {
        AlSource = new AlSourceViewerViewModel(
            EditorServicesLocator.LoadService ?? new AlSourceLoadService())
        {
            IsPanelVisible = true
        };
    }

    partial void OnContentKindChanged(HomeContentKind value) => NotifyVisibility();
    partial void OnLayoutKindChanged(LayoutKind value) => NotifyVisibility();

    private void NotifyVisibility()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsShowingLayout));
        OnPropertyChanged(nameof(IsShowingAl));
        OnPropertyChanged(nameof(IsRdlcLayout));
        OnPropertyChanged(nameof(IsExcelLayout));
        OnPropertyChanged(nameof(IsWordLayout));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanUseCopilot));
        SaveCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        OpenInPreviewCommand.NotifyCanExecuteChanged();
        ToggleCopilotCommand.NotifyCanExecuteChanged();

        if (!CanUseCopilot && IsCopilotOpen)
            IsCopilotOpen = false;
    }

    public void Reset()
    {
        _loadCts?.Cancel();
        LayoutKind = LayoutKind.None;
        FileTitle = string.Empty;
        FilePath = string.Empty;
        FormatDisplay = string.Empty;
        Summary = string.Empty;
        IsDirty = false;
        ContentKind = HomeContentKind.Idle;
        StatusMessage = "Select a layout or AL report from the explorer.";
        AlSource.Clear();
        IsBusy = false;
        IsAgentEditing = false;
        LayoutEditorLoadRequested?.Invoke();
    }

    public void MarkDirty()
    {
        if ((!IsExcelLayout && !IsRdlcLayout) || IsDirty)
            return;

        IsDirty = true;
        UpdateDirtyStatus();
    }

    public void MarkSaved()
    {
        IsDirty = false;
        UpdateDirtyStatus();
        StatusMessage = $"Saved {FileTitle}";
    }

    public void MarkLoaded(string summary)
    {
        IsDirty = false;
        Summary = summary;
        StatusMessage = summary;
        UpdateDirtyStatus();
    }

    /// <summary>Clears status/summary text (used for RDLC visual layout).</summary>
    public void ClearStatus()
    {
        Summary = string.Empty;
        StatusMessage = string.Empty;
        IsDirty = false;
        UpdateDirtyStatus();
    }

    public void ReportEditorError(string message) => StatusMessage = message;

    private void UpdateDirtyStatus()
    {
        FileTitle = string.IsNullOrWhiteSpace(FilePath)
            ? string.Empty
            : Path.GetFileName(FilePath) + (IsDirty ? " *" : string.Empty);
    }

    public async Task OpenAsync(WorkspaceFileEntry entry)
    {
        if (entry.Kind == WorkspaceFileKind.Report)
            await OpenAlAsync(entry.FullPath);
        else
            await OpenLayoutAsync(entry.FullPath);
    }

    public async Task OpenLayoutAsync(string path)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        var kind = LayoutFileHelper.DetectKind(path);
        if (kind == LayoutKind.None)
            throw new NotSupportedException($"Unsupported layout: {Path.GetExtension(path)}");

        try
        {
            IsBusy = true;
            ContentKind = HomeContentKind.Layout;
            AlSource.Clear();
            IsDirty = false;

            FilePath = path;
            FormatDisplay = LayoutFileHelper.FormatLabel(path);
            FileTitle = Path.GetFileName(path);
            LayoutKind = kind;

            if (kind == LayoutKind.Rdlc)
            {
                StatusMessage = string.Empty;
                Summary = string.Empty;
                NotifyVisibility();
                LayoutEditorLoadRequested?.Invoke();
            }
            else
            {
                // Excel (ReoGrid) and Word (Open XML FlowDocument) are hosted in HomeView code-behind.
                StatusMessage = kind == LayoutKind.Excel
                    ? "Loading Excel layout…"
                    : "Loading Word layout…";
                Summary = kind == LayoutKind.Excel
                    ? "Edit with ReoGrid · Save writes .xlsx"
                    : "Open XML preview · edit in Microsoft Word";
                NotifyVisibility();
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                LayoutEditorLoadRequested?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // superseded
        }
        catch (Exception ex)
        {
            Reset();
            StatusMessage = $"Failed to load layout: {ex.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;
            NotifyVisibility();
        }
    }

    public async Task OpenAlAsync(string path)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        try
        {
            IsBusy = true;
            StatusMessage = "Loading AL source…";
            ContentKind = HomeContentKind.AlSource;
            LayoutKind = LayoutKind.None;
            FormatDisplay = "AL";
            FilePath = path;
            FileTitle = Path.GetFileName(path);
            Summary = Path.GetDirectoryName(path) ?? string.Empty;
            IsDirty = false;
            LayoutEditorLoadRequested?.Invoke();

            await AlSource.LoadAsync(path, token);
            token.ThrowIfCancellationRequested();
            StatusMessage = AlSource.HasSource
                ? $"{AlSource.FileName} · {AlSource.LineCount} lines"
                : AlSource.StatusMessage;
        }
        catch (OperationCanceledException)
        {
            // superseded
        }
        catch (Exception ex)
        {
            Reset();
            StatusMessage = $"Failed to load AL source: {ex.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;
            NotifyVisibility();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!CanSave)
            return;
        LayoutEditorSaveRequested?.Invoke();
    }

    [RelayCommand]
    private async Task Reload()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
            return;
        await OpenLayoutAsync(FilePath);
    }

    [RelayCommand(CanExecute = nameof(IsRdlcLayout))]
    private void OpenInPreview()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || LayoutKind != LayoutKind.Rdlc)
            return;

        OpenLayoutInPreviewRequested?.Invoke(FilePath);
    }

    [RelayCommand(CanExecute = nameof(CanUseCopilot))]
    private void ToggleCopilot() => IsCopilotOpen = !IsCopilotOpen;
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReportExpert.Core.Editors;
using ReportExpert.Editors.Models;

namespace ReportExpert.Editors.ViewModels;

public partial class AlSourceViewerViewModel : ObservableObject
{
    private readonly IAlSourceLoadService _loadService;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _documentText = string.Empty;

    [ObservableProperty]
    private int _lineCount;

    [ObservableProperty]
    private AlSourceLoadState _loadState = AlSourceLoadState.Idle;

    [ObservableProperty]
    private string _statusMessage = "Open a Report.al file to inspect the dataset definition.";

    [ObservableProperty]
    private bool _isPanelVisible = true;

    public bool HasSource => LoadState == AlSourceLoadState.Loaded;

    public string FileName => string.IsNullOrWhiteSpace(FilePath)
        ? "No AL file"
        : System.IO.Path.GetFileName(FilePath);

    public event EventHandler<string>? DocumentTextChanged;
    public event EventHandler<int>? GoToLineRequested;

    public AlSourceViewerViewModel(IAlSourceLoadService loadService)
    {
        _loadService = loadService;
    }

    public async Task LoadAsync(string? path, CancellationToken cancellationToken = default)
    {
        _loadCts?.Cancel();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _loadCts.Token;

        if (string.IsNullOrWhiteSpace(path))
        {
            Clear();
            LoadState = AlSourceLoadState.NotFound;
            StatusMessage = "No paired AL source was found for this report.";
            return;
        }

        try
        {
            LoadState = AlSourceLoadState.Loading;
            StatusMessage = "Loading AL source...";
            FilePath = path;

            var document = await _loadService.LoadAsync(path, token).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();

            if (!document.Exists)
            {
                DocumentText = string.Empty;
                LineCount = 0;
                LoadState = AlSourceLoadState.NotFound;
                StatusMessage = document.ErrorMessage ?? "AL source file was not found.";
                return;
            }

            DocumentText = document.Content;
            LineCount = document.LineCount;
            LoadState = AlSourceLoadState.Loaded;
            StatusMessage = $"{FileName} ({LineCount} lines)";
            DocumentTextChanged?.Invoke(this, document.Content);
        }
        catch (OperationCanceledException)
        {
            // Superseded load — reset so the empty/loading overlays do not stick forever.
            if (LoadState == AlSourceLoadState.Loading)
            {
                LoadState = AlSourceLoadState.Idle;
                StatusMessage = "Open a Report.al file to inspect the dataset definition.";
            }
        }
        catch (Exception ex)
        {
            DocumentText = string.Empty;
            LineCount = 0;
            LoadState = AlSourceLoadState.Error;
            StatusMessage = ex.Message;
        }
    }

    public void Clear()
    {
        FilePath = string.Empty;
        DocumentText = string.Empty;
        LineCount = 0;
        LoadState = AlSourceLoadState.Idle;
        StatusMessage = "Open a Report.al file to inspect the dataset definition.";
    }

    public void GoToLine(int lineNumber) => GoToLineRequested?.Invoke(this, lineNumber);

    [RelayCommand]
    private async Task OpenAlAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "AL source (*.al;*.Report.al)|*.al;*.Report.al|All files (*.*)|*.*",
            Title = "Open Report.al"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await LoadAsync(dialog.FileName);
    }

    [RelayCommand(CanExecute = nameof(HasSource))]
    private async Task ReloadAsync()
    {
        if (!string.IsNullOrWhiteSpace(FilePath))
        {
            await LoadAsync(FilePath);
        }
    }

    partial void OnLoadStateChanged(AlSourceLoadState value)
    {
        OnPropertyChanged(nameof(HasSource));
        ReloadCommand.NotifyCanExecuteChanged();
    }
}

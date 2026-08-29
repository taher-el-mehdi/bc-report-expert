using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Search;
using ReportExpert.Core.Editors;
using ReportExpert.Editors.Folding;
using ReportExpert.Editors.ViewModels;

namespace ReportExpert.Editors.Editors;

public partial class AlSourceViewer : UserControl
{
    private readonly AlFoldingStrategy _foldingStrategy = new();
    private FoldingManager? _foldingManager;
    private ITextMateEditorSession? _textMateSession;
    private bool _isEditorInitialized;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(AlSourceViewerViewModel),
            typeof(AlSourceViewer),
            new PropertyMetadata(null, OnViewModelChanged));

    public AlSourceViewerViewModel? ViewModel
    {
        get => (AlSourceViewerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public AlSourceViewer()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<int>? CaretLineChanged;

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AlSourceViewer viewer)
        {
            // Do NOT replace the control's DataContext — that breaks parent Visibility bindings
            // (e.g. HomeViewer.IsShowingAl). Inner content binds via ViewModel on ElementName=Root.
            viewer.WireViewModel(e.NewValue as AlSourceViewerViewModel, e.OldValue as AlSourceViewerViewModel);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // If host set DataContext to the VM directly, mirror it into the ViewModel DP.
        if (ViewModel == null && e.NewValue is AlSourceViewerViewModel vm)
            ViewModel = vm;
    }

    private void WireViewModel(AlSourceViewerViewModel? newVm, AlSourceViewerViewModel? oldVm)
    {
        if (oldVm != null)
        {
            oldVm.DocumentTextChanged -= OnDocumentTextChanged;
            oldVm.GoToLineRequested -= OnGoToLineRequested;
        }

        if (newVm != null)
        {
            newVm.DocumentTextChanged += OnDocumentTextChanged;
            newVm.GoToLineRequested += OnGoToLineRequested;
            ApplyDocumentText(newVm.DocumentText);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isEditorInitialized)
        {
            return;
        }

        _isEditorInitialized = true;
        ConfigureEditor();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _textMateSession?.Dispose();
        _textMateSession = null;
    }

    private void ConfigureEditor()
    {
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableRectangularSelection = false;
        Editor.Options.AllowScrollBelowDocument = true;

        SearchPanel.Install(Editor);
        _foldingManager = FoldingManager.Install(Editor.TextArea);

        if (EditorServicesLocator.SyntaxHighlighting != null)
        {
            _textMateSession = EditorServicesLocator.SyntaxHighlighting.Attach(Editor);
        }

        Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        Editor.TextChanged += Editor_TextChanged;

        if (ViewModel != null)
        {
            ApplyDocumentText(ViewModel.DocumentText);
        }
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_foldingManager != null)
        {
            _foldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        CaretLineChanged?.Invoke(this, Editor.TextArea.Caret.Line);
    }

    private void OnDocumentTextChanged(object? sender, string content) => ApplyDocumentText(content);

    private void OnGoToLineRequested(object? sender, int lineNumber) => _textMateSession?.GoToLine(lineNumber);

    private void ApplyDocumentText(string content)
    {
        if (!_isEditorInitialized)
        {
            return;
        }

        Editor.Document.Text = content;
        if (_foldingManager != null)
        {
            _foldingStrategy.UpdateFoldings(_foldingManager, Editor.Document);
        }
    }
}

/// <summary>Lightweight service locator used until full app DI wiring is complete.</summary>
public static class EditorServicesLocator
{
    public static IAlSyntaxHighlightingService? SyntaxHighlighting { get; set; }
    public static IEditorThemeService? ThemeService { get; set; }
    public static IAlSourceLoadService? LoadService { get; set; }
    public static IAlSourcePathResolver? PathResolver { get; set; }
}

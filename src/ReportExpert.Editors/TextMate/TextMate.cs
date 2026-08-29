using System.Buffers;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using TextMateSharp.Grammars;
using TextMateSharp.Model;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using FontStyle = TextMateSharp.Themes.FontStyle;

namespace ReportExpert.Editors.TextMate;

public sealed class TextMateColoringTransformer :
    GenericLineTransformer,
    IModelTokensChangedListener,
    IDisposable
{
    private readonly object _lock = new();
    private bool _isDisposed;
    private Theme? _theme;
    private IGrammar? _grammar;
    private TMModel? _model;
    private TextDocument? _document;
    private readonly TextView _textView;
    private readonly Action<Exception>? _exceptionHandler;
    private bool _areVisualLinesValid;
    private int _firstVisibleLineIndex = -1;
    private int _lastVisibleLineIndex = -1;
    private Dictionary<int, Brush> _brushes = new();

    public TextMateColoringTransformer(TextView textView, Action<Exception>? exceptionHandler)
        : base(exceptionHandler)
    {
        _textView = textView ?? throw new ArgumentNullException(nameof(textView));
        _exceptionHandler = exceptionHandler;
        _textView.VisualLinesChanged += TextView_VisualLinesChanged;
    }

    public void SetModel(TextDocument? document, TMModel? model)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            ThrowIfDisposed();
            _areVisualLinesValid = false;
            _document = document;
            _model = model;

            if (_grammar != null && _model != null)
            {
                _model.SetGrammar(_grammar);
            }
        }
    }

    private void TextView_VisualLinesChanged(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _isDisposed))
        {
            return;
        }

        try
        {
            if (!_textView.VisualLinesValid || _textView.VisualLines.Count == 0)
            {
                return;
            }

            lock (_lock)
            {
                if (Volatile.Read(ref _isDisposed))
                {
                    return;
                }

                _areVisualLinesValid = true;
                _firstVisibleLineIndex = _textView.VisualLines[0].FirstDocumentLine.LineNumber - 1;
                _lastVisibleLineIndex = _textView.VisualLines[^1].LastDocumentLine.LineNumber - 1;
            }
        }
        catch (Exception ex)
        {
            _exceptionHandler?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _isDisposed))
        {
            return;
        }

        lock (_lock)
        {
            if (Volatile.Read(ref _isDisposed))
            {
                return;
            }

            Volatile.Write(ref _isDisposed, true);
            _theme = null;
            _grammar = null;
            _model = null;
            _document = null;
            _brushes = new Dictionary<int, Brush>();
        }

        _textView.VisualLinesChanged -= TextView_VisualLinesChanged;
    }

    public void SetTheme(Theme theme)
    {
        ThrowIfDisposed();

        var map = theme.GetColorMap();
        var newBrushes = new Dictionary<int, Brush>();

        foreach (var color in map)
        {
            var id = theme.GetColorId(color);
            newBrushes[id] = new SolidColorBrush(TextMateColorHelper.Parse(color));
        }

        lock (_lock)
        {
            ThrowIfDisposed();
            _theme = theme;
            _brushes = newBrushes;
        }
    }

    public void SetGrammar(IGrammar? grammar)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            ThrowIfDisposed();
            _grammar = grammar;
            _model?.SetGrammar(grammar);
        }
    }

    protected override void TransformLine(DocumentLine line, ITextRunConstructionContext context)
    {
        if (Volatile.Read(ref _isDisposed))
        {
            return;
        }

        try
        {
            TMModel? model;
            TextDocument? document;
            Theme? theme;
            Dictionary<int, Brush> brushes;

            lock (_lock)
            {
                if (Volatile.Read(ref _isDisposed))
                {
                    return;
                }

                model = _model;
                document = _document;
                theme = _theme;
                brushes = _brushes;
            }

            if (model == null || document == null || theme == null || brushes.Count == 0)
            {
                return;
            }

            int lineNumber = line.LineNumber;
            var tokens = model.GetLineTokens(lineNumber - 1);
            if (tokens == null || tokens.Count == 0)
            {
                return;
            }

            var transformsInLine = ArrayPool<ForegroundTextTransformation>.Shared.Rent(tokens.Count);

            try
            {
                GetLineTransformations(lineNumber, tokens, transformsInLine, model, document, theme, brushes);

                for (int i = 0; i < tokens.Count; i++)
                {
                    transformsInLine[i]?.Transform(this, line);
                }
            }
            finally
            {
                ArrayPool<ForegroundTextTransformation>.Shared.Return(transformsInLine);
            }
        }
        catch (Exception ex)
        {
            _exceptionHandler?.Invoke(ex);
        }
    }

    private static void GetLineTransformations(
        int lineNumber,
        List<TMToken> tokens,
        ForegroundTextTransformation[] transformations,
        TMModel model,
        TextDocument document,
        Theme theme,
        Dictionary<int, Brush> brushes)
    {
        var lineOffset = document.GetLineByNumber(lineNumber).Offset;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var nextToken = i + 1 < tokens.Count ? tokens[i + 1] : null;
            var startIndex = token.StartIndex;
            var endIndex = nextToken?.StartIndex ?? model.GetLines().GetLineLength(lineNumber - 1);

            if (startIndex >= endIndex || token.Scopes == null || token.Scopes.Count == 0)
            {
                transformations[i] = null;
                continue;
            }

            int foreground = 0;
            int background = 0;
            FontStyle fontStyle = 0;

            foreach (var themeRule in theme.Match(token.Scopes))
            {
                if (foreground == 0 && themeRule.foreground > 0)
                {
                    foreground = themeRule.foreground;
                }

                if (background == 0 && themeRule.background > 0)
                {
                    background = themeRule.background;
                }

                if (fontStyle == 0 && themeRule.fontStyle > 0)
                {
                    fontStyle = themeRule.fontStyle;
                }
            }

            transformations[i] ??= new ForegroundTextTransformation();
            transformations[i].ColorMap = brushes;
            transformations[i].ExceptionHandler = null;
            transformations[i].StartOffset = lineOffset + startIndex;
            transformations[i].EndOffset = lineOffset + endIndex;
            transformations[i].ForegroundColor = foreground;
            transformations[i].BackgroundColor = background;
            transformations[i].FontStyle = fontStyle;
        }
    }

    public void ModelTokensChanged(ModelTokensChangedEvent e)
    {
        if (e.Ranges == null || Volatile.Read(ref _isDisposed))
        {
            return;
        }

        TMModel? model;
        TextDocument? document;
        bool areVisualLinesValid;
        int firstVisibleLineIndex;
        int lastVisibleLineIndex;

        lock (_lock)
        {
            if (Volatile.Read(ref _isDisposed))
            {
                return;
            }

            model = _model;
            document = _document;
            areVisualLinesValid = _areVisualLinesValid;
            firstVisibleLineIndex = _firstVisibleLineIndex;
            lastVisibleLineIndex = _lastVisibleLineIndex;
        }

        if (model == null || model.IsStopped || document == null)
        {
            return;
        }

        int firstChangedLineIndex = int.MaxValue;
        int lastChangedLineIndex = -1;

        foreach (var range in e.Ranges)
        {
            firstChangedLineIndex = Math.Min(range.FromLineNumber - 1, firstChangedLineIndex);
            lastChangedLineIndex = Math.Max(range.ToLineNumber - 1, lastChangedLineIndex);
        }

        if (areVisualLinesValid)
        {
            bool changedLinesAreNotVisible =
                (firstChangedLineIndex < firstVisibleLineIndex && lastChangedLineIndex < firstVisibleLineIndex) ||
                (firstChangedLineIndex > lastVisibleLineIndex && lastChangedLineIndex > lastVisibleLineIndex);

            if (changedLinesAreNotVisible)
            {
                return;
            }
        }

        _textView.Dispatcher.BeginInvoke(() =>
        {
            if (Volatile.Read(ref _isDisposed) || document == null)
            {
                return;
            }

            int firstLineIndexToRedraw = Math.Max(firstChangedLineIndex, firstVisibleLineIndex);
            int lastLineIndexToRedrawLine = Math.Min(lastChangedLineIndex, lastVisibleLineIndex);
            int totalLines = document.Lines.Count - 1;

            firstLineIndexToRedraw = Math.Clamp(firstLineIndexToRedraw, 0, totalLines);
            lastLineIndexToRedrawLine = Math.Clamp(lastLineIndexToRedrawLine, 0, totalLines);

            DocumentLine firstLineToRedraw = document.Lines[firstLineIndexToRedraw];
            DocumentLine lastLineToRedraw = document.Lines[lastLineIndexToRedrawLine];

            _textView.Redraw(
                firstLineToRedraw.Offset,
                lastLineToRedraw.Offset + lastLineToRedraw.TotalLength - firstLineToRedraw.Offset);
        }, DispatcherPriority.Background);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed))
        {
            throw new ObjectDisposedException(nameof(TextMateColoringTransformer));
        }
    }
}

public static class TextMateInstaller
{
    public static Installation InstallTextMate(
        this TextEditor editor,
        IRegistryOptions registryOptions,
        bool initCurrentDocument = true,
        Action<Exception>? exceptionHandler = null) =>
        new(editor, registryOptions, initCurrentDocument, exceptionHandler);

    public sealed class Installation : IDisposable
    {
        private bool _isDisposed;
        private readonly object _lock = new();
        private readonly Registry _textMateRegistry;
        private readonly TextEditor _editor;
        private TextEditorModel? _editorModel;
        private IGrammar? _grammar;
        private TMModel? _tmModel;
        private TextMateColoringTransformer? _transformer;
        private readonly bool _ownsTransformer;
        private ReadOnlyDictionary<string, string>? _themeColorsDictionary;

        public IRegistryOptions RegistryOptions { get; }

        public Installation(
            TextEditor editor,
            IRegistryOptions registryOptions,
            bool initCurrentDocument = true,
            Action<Exception>? exceptionHandler = null)
        {
            RegistryOptions = registryOptions ?? throw new ArgumentNullException(nameof(registryOptions));
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            _textMateRegistry = new Registry(registryOptions);
            _transformer = _editor.TextArea.TextView.LineTransformers
                .OfType<TextMateColoringTransformer>()
                .FirstOrDefault();

            if (_transformer is null)
            {
                _transformer = new TextMateColoringTransformer(_editor.TextArea.TextView, exceptionHandler);
                _editor.TextArea.TextView.LineTransformers.Add(_transformer);
                _ownsTransformer = true;
            }

            SetTheme(registryOptions.GetDefaultTheme());
            _editor.DocumentChanged += OnEditorOnDocumentChanged;

            if (initCurrentDocument)
            {
                OnEditorOnDocumentChanged(_editor, EventArgs.Empty);
            }
        }

        public void SetGrammar(string scopeName)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();
                SetGrammarInternal(_textMateRegistry.LoadGrammar(scopeName));
            }

            _editor.TextArea.TextView.Redraw();
        }

        public void SetGrammarFile(string path)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();
                SetGrammarInternal(_textMateRegistry.LoadGrammarFromPathSync(path, 0, null));
            }

            _editor.TextArea.TextView.Redraw();
        }

        private void SetGrammarInternal(IGrammar? grammar)
        {
            _grammar = grammar;
            _transformer?.SetGrammar(_grammar);
        }

        public void SetTheme(IRawTheme theme)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                ThrowIfDisposed();

                _textMateRegistry.SetTheme(theme);
                var registryTheme = _textMateRegistry.GetTheme();
                _transformer?.SetTheme(registryTheme);
                _tmModel?.InvalidateLine(0);
                _editorModel?.InvalidateViewPortLines();
                _themeColorsDictionary = registryTheme.GetGuiColorDictionary();
            }
        }

        public bool TryGetThemeColor(string colorKey, out string colorString)
        {
            ThrowIfDisposed();
            var dict = Volatile.Read(ref _themeColorsDictionary)
                ?? throw new ObjectDisposedException(nameof(Installation));
            return dict.TryGetValue(colorKey, out colorString!);
        }

        public void Dispose()
        {
            if (Volatile.Read(ref _isDisposed))
            {
                return;
            }

            TextEditorModel? editorModel;
            TMModel? tmModel;
            TextMateColoringTransformer? transformer;

            lock (_lock)
            {
                if (Volatile.Read(ref _isDisposed))
                {
                    return;
                }

                Volatile.Write(ref _isDisposed, true);
                editorModel = _editorModel;
                _editorModel = null;
                tmModel = _tmModel;
                _tmModel = null;
                transformer = _transformer;
                _transformer = null;
                _grammar = null;
                _themeColorsDictionary = null;
            }

            _editor.DocumentChanged -= OnEditorOnDocumentChanged;
            editorModel?.Dispose();

            if (tmModel != null)
            {
                if (transformer != null)
                {
                    tmModel.RemoveModelTokensChangedListener(transformer);
                }

                tmModel.Dispose();
            }

            if (_ownsTransformer && transformer != null)
            {
                _editor.TextArea.TextView.LineTransformers.Remove(transformer);
                transformer.Dispose();
            }
            else if (transformer != null)
            {
                transformer.SetModel(null, null);
            }
        }

        private void OnEditorOnDocumentChanged(object? sender, EventArgs args)
        {
            if (Volatile.Read(ref _isDisposed))
            {
                return;
            }

            lock (_lock)
            {
                if (Volatile.Read(ref _isDisposed))
                {
                    return;
                }

                _editorModel?.Dispose();
                if (_tmModel != null && _transformer != null)
                {
                    _tmModel.RemoveModelTokensChangedListener(_transformer);
                    _tmModel.Dispose();
                }

                _editorModel = new TextEditorModel(_editor.TextArea.TextView, _editor.Document, null);
                _tmModel = new TMModel(_editorModel);
                _tmModel.SetGrammar(_grammar);
                _transformer?.SetModel(_editor.Document, _tmModel);
                _tmModel.AddModelTokensChangedListener(_transformer!);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _isDisposed))
            {
                throw new ObjectDisposedException(nameof(Installation));
            }
        }
    }
}

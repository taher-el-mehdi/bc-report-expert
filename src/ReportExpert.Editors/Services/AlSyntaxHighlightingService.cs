using System.IO;
using ICSharpCode.AvalonEdit;
using ReportExpert.Core.Editors;
using ReportExpert.Editors.TextMate;
using TextMateSharp.Grammars;

namespace ReportExpert.Editors.Services;

public sealed class AlSyntaxHighlightingService : IAlSyntaxHighlightingService
{
    private readonly EditorThemeService _themeService;

    public AlSyntaxHighlightingService(IEditorThemeService themeService)
    {
        _themeService = themeService as EditorThemeService
            ?? throw new ArgumentException("Expected EditorThemeService implementation.", nameof(themeService));
    }

    public ITextMateEditorSession Attach(object textEditor)
    {
        if (textEditor is not TextEditor editor)
        {
            throw new ArgumentException("Expected AvalonEdit TextEditor.", nameof(textEditor));
        }

        var registry = new RegistryOptions(_themeService.GetThemeName());
        TextMateInstaller.Installation installation = editor.InstallTextMate(registry, initCurrentDocument: true);
        string grammarFile = Path.Combine(AlGrammarResourceCache.EnsureGrammarDirectory(), "al.tmLanguage.json");
        installation.SetGrammarFile(grammarFile);
        _themeService.ApplyEditorChrome(editor, installation);

        return new TextMateSession(editor, installation, _themeService);
    }

    public void RefreshAllThemes() => _themeService.NotifyThemeChanged();
}

internal sealed class TextMateSession : ITextMateEditorSession
{
    private readonly TextEditor _editor;
    private readonly TextMateInstaller.Installation _installation;
    private readonly EditorThemeService _themeService;

    public TextMateSession(TextEditor editor, TextMateInstaller.Installation installation, EditorThemeService themeService)
    {
        _editor = editor;
        _installation = installation;
        _themeService = themeService;
    }

    public void SetThemeFromApp()
    {
        var registry = new RegistryOptions(_themeService.GetThemeName());
        _installation.SetTheme(registry.GetDefaultTheme());
        _themeService.ApplyEditorChrome(_editor, _installation);
        _editor.TextArea.TextView.Redraw();
    }

    public void GoToLine(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > _editor.Document.LineCount)
        {
            return;
        }

        var line = _editor.Document.GetLineByNumber(lineNumber);
        _editor.CaretOffset = line.Offset;
        _editor.ScrollToLine(lineNumber);
    }

    public void Dispose() => _installation.Dispose();
}

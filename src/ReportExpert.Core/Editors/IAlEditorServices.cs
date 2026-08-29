namespace ReportExpert.Core.Editors;

public interface IAlSourceLoadService
{
    Task<AlSourceDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}

public interface IAlSourcePathResolver
{
    string? ResolveFromRdlc(string rdlcPath);
}

public interface IAlSyntaxHighlightingService
{
    ITextMateEditorSession Attach(object textEditor);

    void RefreshAllThemes();
}

public interface IEditorThemeService
{
    event EventHandler? ThemeChanged;

    bool IsDarkTheme { get; }

    void NotifyThemeChanged();
}

public sealed class AlSourceDocument
{
    public required string FilePath { get; init; }
    public required string Content { get; init; }
    public int LineCount { get; init; }
    public bool Exists { get; init; }
    public string? ErrorMessage { get; init; }

    public static AlSourceDocument NotFound(string? path) => new()
    {
        FilePath = path ?? string.Empty,
        Content = string.Empty,
        LineCount = 0,
        Exists = false,
        ErrorMessage = string.IsNullOrWhiteSpace(path) ? null : "AL source file was not found."
    };

    public static AlSourceDocument FromContent(string path, string content) => new()
    {
        FilePath = path,
        Content = content,
        LineCount = string.IsNullOrEmpty(content) ? 0 : content.Split('\n').Length,
        Exists = true
    };
}

public interface ITextMateEditorSession : IDisposable
{
    void SetThemeFromApp();

    void GoToLine(int lineNumber);
}

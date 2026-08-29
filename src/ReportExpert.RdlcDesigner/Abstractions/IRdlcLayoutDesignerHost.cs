using System.Windows;

namespace ReportExpert.RdlcDesigner.Abstractions;

/// <summary>
/// App-facing host for the Minimal RDLC layout designer.
/// </summary>
public interface IRdlcLayoutDesignerHost
{
    FrameworkElement View { get; }
    bool IsDirty { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    string? FilePath { get; }
    double PageWidthPx { get; }

    event EventHandler? DirtyChanged;
    event EventHandler? DocumentChanged;
    event EventHandler? UndoStateChanged;

    void Load(string path);
    void Save(string? path = null);
    void Undo();
    void Redo();
    void Clear();

    /// <summary>Applies designer chrome settings (rulers / gridlines).</summary>
    void ApplyViewSettings(bool showRulers, bool showGridlines);
}

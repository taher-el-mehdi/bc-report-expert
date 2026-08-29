using System.Windows;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.ContextMenus;
using ReportExpert.RdlcDesigner.Extensibility;
using ReportExpert.RdlcDesigner.Model;
using ReportExpert.RdlcDesigner.Persistence;
using ReportExpert.RdlcDesigner.Properties;
using ReportExpert.RdlcDesigner.Services;
using ReportExpert.RdlcDesigner.Surface;
using ReportExpert.RdlcDesigner.Views;

namespace ReportExpert.RdlcDesigner.Hosting;

/// <summary>
/// Default host wiring persistence, surface, selection, undo, schema, toolbox, and edits.
/// </summary>
public sealed class RdlcLayoutDesignerHost : IRdlcLayoutDesignerHost
{
    private readonly IRdlcPersistence _persistence;
    private readonly UndoService _undo = new();
    private readonly SelectionService _selection = new();
    private readonly ToolboxService _toolbox = new();
    private readonly RdlcLayoutDesignerView _view = new();
    private readonly DesignSurface _surface;
    private readonly PropertyService _properties;
    private readonly SchemaService _schema;
    private readonly DocumentEditService _edits;
    private readonly ReportItemFactory _factory;
    private readonly PropertyBrowserService _propertyBrowser;
    private RdlcDocument? _document;
    private bool _dirty;
    private string? _selectedId;
    private bool _applyingXml;
    private string? _lastAppliedXml;

    public RdlcLayoutDesignerHost(IRdlcPersistence? persistence = null)
    {
        _persistence = persistence ?? new RdlcPersistence();
        _properties = new PropertyService(() => _document, _undo, OnModelMutated);
        _schema = new SchemaService(() => _document, OnSchemaMutated);
        _edits = new DocumentEditService(() => _document, _selection, _properties, _undo, OnModelMutated);
        _factory = new ReportItemFactory(_edits);
        _propertyBrowser = new PropertyBrowserService(() => _document, _selection, _properties);
        _surface = new DesignSurface(_selection, _properties);

        _view.AttachServices(
            _surface,
            _selection,
            _properties,
            _edits,
            _schema,
            _toolbox,
            _factory,
            _propertyBrowser,
            Undo,
            Redo,
            () => CanUndo,
            () => CanRedo,
            GetXmlText,
            TryApplyXmlText);

        var contextMenus = new DesignerContextMenuBuilder(
            _selection,
            _edits,
            _properties,
            _schema,
            (kind, point) => _factory.Create(kind, point.X, point.Y),
            openExpression: _view.OpenExpressionEditorPublic,
            undo: Undo,
            redo: Redo);

        _surface.AttachInteractions(_factory, _toolbox, contextMenus);
        _view.PreviewKeyDown += OnPreviewKeyDown;
        _undo.StateChanged += (_, _) =>
        {
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
            _view.RefreshCommandState();
        };
        _selection.SelectionChanged += (_, _) =>
            _selectedId = _selection.Primary?.Id;
    }

    public FrameworkElement View => _view;
    public bool IsDirty => _dirty;
    public bool CanUndo => _undo.CanUndo;
    public bool CanRedo => _undo.CanRedo;
    public string? FilePath => _document?.FilePath;
    public double PageWidthPx => _document?.PageWidthPx ?? _surface.PageWidthPx;

    public event EventHandler? DirtyChanged;
    public event EventHandler? DocumentChanged;
    public event EventHandler? UndoStateChanged;

    public void Load(string path)
    {
        _document = _persistence.Load(path);
        _undo.Clear();
        _selection.Clear();
        SetDirty(false);
        _surface.Bind(_document);
        _schema.Refresh();
        _propertyBrowser.CreateContext();
        _lastAppliedXml = null;
        _view.SetStatus($"Loaded {System.IO.Path.GetFileName(path)}");
        _view.RefreshCommandState();
        SyncXmlView(force: true);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save(string? path = null)
    {
        if (_document is null)
            throw new InvalidOperationException("No document loaded.");

        _persistence.Save(_document, path);
        SetDirty(false);
        _view.SetStatus($"Saved {System.IO.Path.GetFileName(_document.FilePath)}");
        SyncXmlView(force: true);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!_undo.CanUndo)
            return;
        _undo.Undo();
        _schema.Refresh();
        RefreshSurfacePreserveSelection();
        SetDirty(true);
        _view.RefreshCommandState();
        SyncXmlView();
    }

    public void Redo()
    {
        if (!_undo.CanRedo)
            return;
        _undo.Redo();
        _schema.Refresh();
        RefreshSurfacePreserveSelection();
        SetDirty(true);
        _view.RefreshCommandState();
        SyncXmlView();
    }

    public void Clear()
    {
        _document = null;
        _undo.Clear();
        _selection.Clear();
        _surface.Refresh();
        _schema.Refresh();
        _lastAppliedXml = null;
        SetDirty(false);
        _view.SetStatus("Ready");
        SyncXmlView(force: true);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyViewSettings(bool showRulers, bool showGridlines) =>
        _view.ApplyViewSettings(showRulers, showGridlines);

    private string GetXmlText() =>
        _document is null ? string.Empty : RdlcXmlFormatter.Format(_document.Xml);

    /// <summary>
    /// Applies edited XML to the live document. Returns null on success, or an error message.
    /// </summary>
    private string? TryApplyXmlText(string xmlText)
    {
        if (string.IsNullOrWhiteSpace(xmlText))
            return "XML is empty.";

        // Skip no-op re-applies (e.g. after design→xml push).
        if (string.Equals(xmlText, _lastAppliedXml, StringComparison.Ordinal))
            return null;

        XDocument parsed;
        try
        {
            parsed = XDocument.Parse(xmlText, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            return $"Invalid XML (line {ex.LineNumber}): {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Invalid XML: {ex.Message}";
        }

        if (parsed.Root is null || parsed.Root.Name.LocalName != "Report")
            return "Root element must be <Report>.";

        try
        {
            _applyingXml = true;
            string? path = _document?.FilePath;
            _document = new RdlcDocument(parsed, path ?? string.Empty);
            _undo.Clear();
            _selection.Clear();
            _surface.Bind(_document);
            _schema.Refresh();
            _propertyBrowser.CreateContext();
            _lastAppliedXml = RdlcXmlFormatter.Format(_document.Xml);
            SetDirty(true);
            _view.RefreshCommandState();
            DocumentChanged?.Invoke(this, EventArgs.Empty);
            UndoStateChanged?.Invoke(this, EventArgs.Empty);
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not load report: {ex.Message}";
        }
        finally
        {
            _applyingXml = false;
        }
    }

    private void SyncXmlView(bool force = false)
    {
        if (_applyingXml)
            return;
        _lastAppliedXml = _document is null ? null : RdlcXmlFormatter.Format(_document.Xml);
        // While editing XML, keep the editor text; refresh when leaving/entering XML or on load.
        _view.RefreshXmlFromDocument(force || !_view.IsXmlMode);
    }

    private void OnSchemaMutated()
    {
        SetDirty(true);
        _view.SetStatus("Parameters updated");
        SyncXmlView();
    }

    private void OnModelMutated()
    {
        SetDirty(true);
        RefreshSurfacePreserveSelection();
        _view.RefreshCommandState();
        SyncXmlView();
    }

    private void RefreshSurfacePreserveSelection()
    {
        var ids = _selection.Selected.Select(s => s.Id).ToList();
        string? primaryId = _selectedId ?? _selection.Primary?.Id;
        _surface.Refresh();
        if (ids.Count == 0)
            return;

        var restored = ids
            .Select(id => _document?.Find(id))
            .Where(i => i is not null)
            .Cast<IReportItem>()
            .ToList();
        if (restored.Count == 0)
            return;

        IReportItem? primary = primaryId is not null
            ? restored.FirstOrDefault(i => string.Equals(i.Id, primaryId, StringComparison.Ordinal))
            : null;
        _selection.SelectMany(restored, primary);
    }

    private void SetDirty(bool value)
    {
        if (_dirty == value)
            return;
        _dirty = value;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.S)
        {
            if (_document?.FilePath is not null)
            {
                Save();
                e.Handled = true;
            }
        }
    }
}

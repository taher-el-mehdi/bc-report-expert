using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Extensibility;
using ReportExpert.RdlcDesigner.Properties;
using ReportExpert.RdlcDesigner.Surface;
using ReportExpert.RdlcDesigner.Theming;
using Wpf.Ui.Appearance;

namespace ReportExpert.RdlcDesigner.Views;

public partial class RdlcLayoutDesignerView : UserControl
{
    private ISelectionService? _selection;
    private IPropertyService? _properties;
    private IDocumentEditService? _edits;
    private ISchemaService? _schema;
    private IToolboxService? _toolbox;
    private IReportItemFactory? _factory;
    private DesignSurface? _surface;
    private PropertyBrowserService? _propertyBrowser;
    private readonly PropertyBrowserPanel _propertyPanel = new();
    private Action? _undo;
    private Action? _redo;
    private Func<bool>? _canUndo;
    private Func<bool>? _canRedo;
    private Func<string>? _getXmlText;
    private Func<string, string?>? _applyXmlText;
    private int _insertOffset;
    private Point? _dragStart;
    private bool _xmlMode;
    private bool _suppressXmlTextChanged;
    private bool _xmlEditorConfigured;
    private bool _themeHooked;
    private bool _showRulers = true;
    private FoldingManager? _xmlFoldingManager;
    private readonly XmlFoldingStrategy _xmlFoldingStrategy = new();
    private readonly DispatcherTimer _xmlApplyTimer;

    public RdlcLayoutDesignerView()
    {
        InitializeComponent();
        PropertyHost.Content = _propertyPanel;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _xmlApplyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _xmlApplyTimer.Tick += XmlApplyTimer_Tick;
    }

    public void AttachServices(
        DesignSurface surface,
        ISelectionService selection,
        IPropertyService properties,
        IDocumentEditService edits,
        ISchemaService schema,
        IToolboxService toolbox,
        IReportItemFactory factory,
        PropertyBrowserService propertyBrowser,
        Action undo,
        Action redo,
        Func<bool> canUndo,
        Func<bool> canRedo,
        Func<string> getXmlText,
        Func<string, string?> applyXmlText)
    {
        if (_surface is not null)
            _surface.ZoomChanged -= OnSurfaceZoomChanged;

        _surface = surface;
        SurfaceHost.Content = surface.Root;
        _surface.ZoomChanged += OnSurfaceZoomChanged;

        if (_selection is not null)
            _selection.SelectionChanged -= OnSelectionChanged;
        if (_schema is not null)
            _schema.SchemaChanged -= OnSchemaChanged;
        if (_toolbox is not null)
            _toolbox.ActiveToolChanged -= OnActiveToolChanged;

        _selection = selection;
        _properties = properties;
        _edits = edits;
        _schema = schema;
        _toolbox = toolbox;
        _factory = factory;
        _propertyBrowser = propertyBrowser;
        _undo = undo;
        _redo = redo;
        _canUndo = canUndo;
        _canRedo = canRedo;
        _getXmlText = getXmlText;
        _applyXmlText = applyXmlText;

        _selection.SelectionChanged += OnSelectionChanged;
        _schema.SchemaChanged += OnSchemaChanged;
        _toolbox.ActiveToolChanged += OnActiveToolChanged;

        BuildToolboxUi();
        _propertyPanel.Attach(_propertyBrowser, OpenExpressionEditor);
        RefreshSchemaUi();
        RefreshCommandState();
        RefreshXmlFromDocument();
        ApplyDesignerTheme();
        UpdateZoomLabel();
        SyncRulers();
    }

    /// <summary>Applies persisted chrome preferences for rulers and gridlines.</summary>
    public void ApplyViewSettings(bool showRulers, bool showGridlines)
    {
        _showRulers = showRulers;
        ApplyRulerVisibility();
        if (_surface is not null)
            _surface.ShowGridlines = showGridlines;
        SyncRulers();
    }

    public void SetStatus(string text) => StatusText.Text = text;

    public bool IsXmlMode => _xmlMode;

    /// <summary>
    /// Pushes the current document XML into the editor (design → XML).
    /// While XML mode is active, skips updates unless <paramref name="force"/> is true
    /// so in-progress edits are not overwritten.
    /// </summary>
    public void RefreshXmlFromDocument(bool force = false)
    {
        if (_getXmlText is null)
            return;
        if (_xmlMode && !force)
            return;

        string xml = _getXmlText();
        _suppressXmlTextChanged = true;
        try
        {
            if (XmlEditor.Document.Text != xml)
                XmlEditor.Document.Text = xml;
            UpdateXmlFoldings();
            XmlStatusText.Text = string.IsNullOrEmpty(xml) ? "No document loaded" : $"{XmlEditor.Document.LineCount} lines";
            XmlStatusText.Foreground = DesignerTheme.MutedForeground;
        }
        finally
        {
            _suppressXmlTextChanged = false;
        }
    }

    public void RefreshCommandState()
    {
        UndoBtn.IsEnabled = _canUndo?.Invoke() == true;
        RedoBtn.IsEnabled = _canRedo?.Invoke() == true;
        DeleteBtn.IsEnabled = !_xmlMode &&
            (_selection?.Primary is { Kind: not ReportItemKind.TablixCellTextbox }
             || (_selection?.Selected.Count > 1));
        ExprBtn.IsEnabled = !_xmlMode && _selection?.Primary?.CanEditValue == true;
        UpdatePropertiesPaneVisibility();
        if (!_xmlMode && PropertiesPane.Visibility == Visibility.Visible)
            _propertyPanel.Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookThemeChanged();
        ApplyDesignerTheme();

        if (_xmlEditorConfigured)
            return;

        _xmlEditorConfigured = true;
        XmlEditor.Options.EnableHyperlinks = false;
        XmlEditor.Options.EnableEmailHyperlinks = false;
        XmlEditor.Options.HighlightCurrentLine = true;
        XmlEditor.Options.AllowScrollBelowDocument = true;
        XmlEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        SearchPanel.Install(XmlEditor);
        _xmlFoldingManager = FoldingManager.Install(XmlEditor.TextArea);
        XmlEditor.TextChanged += XmlEditor_TextChanged;
        UpdateXmlFoldings();
        DesignerTheme.ApplyXmlEditor(XmlEditor);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => UnhookThemeChanged();

    private void HookThemeChanged()
    {
        if (_themeHooked)
            return;
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
        _themeHooked = true;
    }

    private void UnhookThemeChanged()
    {
        if (!_themeHooked)
            return;
        ApplicationThemeManager.Changed -= OnApplicationThemeChanged;
        _themeHooked = false;
    }

    private void OnApplicationThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent) =>
        Dispatcher.BeginInvoke(ApplyDesignerTheme);

    private void ApplyDesignerTheme()
    {
        // Desk around the white paper — theme-aware; the page itself stays white.
        DesignScrollViewer.Background = DesignerTheme.DesignDeskBackground;
        _propertyPanel.ApplyTheme(DesignerTheme.IsDark);
        _surface?.RefreshGridTheme();
        HorizontalRuler.InvalidateVisual();
        VerticalRuler.InvalidateVisual();

        if (_xmlEditorConfigured)
            DesignerTheme.ApplyXmlEditor(XmlEditor);

        HighlightActiveToolboxItem();
        RefreshToolboxCategoryColors();
    }

    private void RefreshToolboxCategoryColors()
    {
        foreach (object child in ToolboxPanel.Children)
        {
            if (child is TextBlock { Tag: "category" } label)
                label.Foreground = DesignerTheme.SecondaryForeground;
        }
    }

    private void DesignMode_Click(object sender, RoutedEventArgs e) => SetViewMode(xmlMode: false);

    private void XmlMode_Click(object sender, RoutedEventArgs e) => SetViewMode(xmlMode: true);

    private void SetViewMode(bool xmlMode)
    {
        _xmlMode = xmlMode;
        DesignModeBtn.IsChecked = !xmlMode;
        XmlModeBtn.IsChecked = xmlMode;
        DesignChrome.Visibility = xmlMode ? Visibility.Collapsed : Visibility.Visible;
        XmlPane.Visibility = xmlMode ? Visibility.Visible : Visibility.Collapsed;
        ApplyXmlBtn.Visibility = xmlMode ? Visibility.Visible : Visibility.Collapsed;
        ZoomInBtn.IsEnabled = !xmlMode;
        ZoomOutBtn.IsEnabled = !xmlMode;
        ZoomResetBtn.IsEnabled = !xmlMode;
        ToolbarHint.Text = xmlMode
            ? "Edit XML · valid changes update the design preview · Ctrl+Enter to apply now"
            : "Drag toolbox items onto the surface · Ctrl+click multi-select · Ctrl+wheel zoom · right-click for menus";

        if (xmlMode)
        {
            RefreshXmlFromDocument(force: true);
            if (_xmlEditorConfigured)
                DesignerTheme.ApplyXmlEditor(XmlEditor);
        }
        else
        {
            _xmlApplyTimer.Stop();
            SyncRulers();
        }

        RefreshCommandState();
    }

    private void ApplyXml_Click(object sender, RoutedEventArgs e) => ApplyXmlNow();

    private void XmlEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressXmlTextChanged || !_xmlMode)
            return;

        UpdateXmlFoldings();
        XmlStatusText.Text = "Applying…";
        XmlStatusText.Foreground = DesignerTheme.MutedForeground;
        _xmlApplyTimer.Stop();
        _xmlApplyTimer.Start();
    }

    private void XmlApplyTimer_Tick(object? sender, EventArgs e)
    {
        _xmlApplyTimer.Stop();
        ApplyXmlNow();
    }

    private void ApplyXmlNow()
    {
        if (_applyXmlText is null)
            return;

        string? error = _applyXmlText(XmlEditor.Document.Text);
        if (error is null)
        {
            XmlStatusText.Text = $"Applied · {XmlEditor.Document.LineCount} lines";
            XmlStatusText.Foreground = DesignerTheme.StatusSuccessForeground;
            SetStatus("XML applied to design preview");
        }
        else
        {
            XmlStatusText.Text = error;
            XmlStatusText.Foreground = DesignerTheme.StatusErrorForeground;
            SetStatus("XML has errors — design preview unchanged");
        }
    }

    private void UpdateXmlFoldings()
    {
        if (_xmlFoldingManager is null)
            return;
        _xmlFoldingStrategy.UpdateFoldings(_xmlFoldingManager, XmlEditor.Document);
    }

    private void UpdatePropertiesPaneVisibility()
    {
        bool hasSelection = !_xmlMode && _selection?.Selected.Count > 0;
        if (hasSelection)
        {
            PropertiesPane.Visibility = Visibility.Visible;
            PropertiesSplitter.Visibility = Visibility.Visible;
            if (PropertiesColumn.Width.Value < 1)
                PropertiesColumn.Width = new GridLength(280);
            PropertiesColumn.MinWidth = 240;
            PropertiesSplitterColumn.Width = new GridLength(4);
        }
        else
        {
            PropertiesPane.Visibility = Visibility.Collapsed;
            PropertiesSplitter.Visibility = Visibility.Collapsed;
            PropertiesColumn.MinWidth = 0;
            PropertiesColumn.Width = new GridLength(0);
            PropertiesSplitterColumn.Width = new GridLength(0);
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => RefreshCommandState();

    private void OnSchemaChanged(object? sender, EventArgs e) => RefreshSchemaUi();

    private void OnActiveToolChanged(object? sender, EventArgs e) => HighlightActiveToolboxItem();

    private void BuildToolboxUi()
    {
        while (ToolboxPanel.Children.Count > 2)
            ToolboxPanel.Children.RemoveAt(2);

        if (_toolbox is null)
            return;

        string? lastCategory = null;
        foreach (ToolboxItem item in _toolbox.Items)
        {
            if (!string.Equals(lastCategory, item.Category, StringComparison.Ordinal))
            {
                lastCategory = item.Category;
                ToolboxPanel.Children.Add(new TextBlock
                {
                    Text = item.Category,
                    Tag = "category",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = DesignerTheme.SecondaryForeground,
                    Margin = new Thickness(4, 10, 0, 4)
                });
            }

            var button = new Button
            {
                Style = (Style)FindResource("ToolboxItemBtn"),
                Tag = item,
                ToolTip = item.Description,
                Content = item.DisplayName
            };
            button.Click += ToolboxItem_Click;
            button.PreviewMouseLeftButtonDown += ToolboxItem_PreviewMouseLeftButtonDown;
            button.PreviewMouseMove += ToolboxItem_PreviewMouseMove;
            ToolboxPanel.Children.Add(button);
        }

        HighlightActiveToolboxItem();
    }

    private void HighlightActiveToolboxItem()
    {
        ToolboxItemKind? active = _toolbox?.ActiveTool;
        foreach (object child in ToolboxPanel.Children)
        {
            if (child is not Button { Tag: ToolboxItem item } btn)
                continue;
            bool isActive = active == item.Kind;
            btn.Background = isActive ? DesignerTheme.ToolboxActiveBackground : Brushes.Transparent;
            btn.BorderBrush = isActive ? DesignerTheme.ToolboxActiveBorder : Brushes.Transparent;
        }
    }

    private void ToolboxItem_Click(object sender, RoutedEventArgs e)
    {
        if (_xmlMode)
            return;
        if (sender is not Button { Tag: ToolboxItem item } || _toolbox is null)
            return;

        _toolbox.SetActiveTool(item.Kind);
        if (!item.CreatesItem)
            return;

        Point p = NextInsertPoint();
        IReportItem? created = _factory?.Create(item.Kind, p.X, p.Y);
        if (created is not null)
        {
            SetStatus($"Inserted {created.Kind} '{created.Name}'");
            _toolbox.SetActiveTool(ToolboxItemKind.Pointer);
        }

        RefreshCommandState();
    }

    private void ToolboxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
    }

    private void ToolboxItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_xmlMode)
            return;
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is null)
            return;
        if (sender is not Button { Tag: ToolboxItem item } || !item.CreatesItem)
            return;

        Point pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.Value.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragStart = null;
        _toolbox?.SetActiveTool(item.Kind);
        DragDrop.DoDragDrop((DependencyObject)sender, ToolboxDragFormats.CreateData(item.Kind), DragDropEffects.Copy);
        _toolbox?.SetActiveTool(ToolboxItemKind.Pointer);
    }

    private Point NextInsertPoint()
    {
        _insertOffset = (_insertOffset + 18) % 120;
        return new Point(24 + _insertOffset, 24 + _insertOffset);
    }

    private void RefreshSchemaUi()
    {
        if (_schema is null)
            return;

        DataTree.Items.Clear();
        foreach (RdlcDataSetNode ds in _schema.DataSets)
        {
            var dsNode = new TreeViewItem { Header = ds.Name, IsExpanded = true, Tag = ds };
            foreach (RdlcFieldNode field in ds.Fields)
            {
                dsNode.Items.Add(new TreeViewItem
                {
                    Header = string.IsNullOrWhiteSpace(field.TypeName)
                        ? field.Name
                        : $"{field.Name}  ({ShortType(field.TypeName)})",
                    Tag = field
                });
            }

            DataTree.Items.Add(dsNode);
        }

        if (_schema.DataSets.Count == 0)
        {
            DataTree.Items.Add(new TreeViewItem
            {
                Header = "(No datasets in this RDLC)",
                IsEnabled = false
            });
        }

        string? selectedParam = (ParametersList.SelectedItem as RdlcParameterNode)?.Name;
        ParametersList.ItemsSource = _schema.Parameters.ToList();
        if (selectedParam is not null)
        {
            ParametersList.SelectedItem = _schema.Parameters
                .FirstOrDefault(p => string.Equals(p.Name, selectedParam, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string ShortType(string typeName)
    {
        int dot = typeName.LastIndexOf('.');
        return dot >= 0 ? typeName[(dot + 1)..] : typeName;
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (_xmlMode || _selection is null || _edits is null)
            return;

        var targets = _selection.Selected
            .Where(i => i.Kind is not ReportItemKind.TablixCellTextbox)
            .ToList();
        if (targets.Count == 0)
            return;

        foreach (IReportItem item in targets)
            _edits.Delete(item);

        SetStatus(targets.Count == 1 ? $"Deleted '{targets[0].Name}'" : $"Deleted {targets.Count} items");
        RefreshCommandState();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _undo?.Invoke();
        RefreshCommandState();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _redo?.Invoke();
        RefreshCommandState();
    }

    private void Expression_Click(object sender, RoutedEventArgs e) => OpenExpressionEditorPublic();

    public void OpenExpressionEditorPublic() => OpenExpressionEditor();

    private void OpenExpressionEditor()
    {
        if (_xmlMode)
            return;
        if (_selection?.Primary is not { CanEditValue: true } item || _properties is null || _schema is null)
            return;

        var dlg = new ExpressionEditorWindow(item.Value, _schema.DataSets, _schema.Parameters)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() == true)
        {
            _properties.SetValue(item, dlg.Expression);
            SetStatus("Expression updated");
            _propertyPanel.Refresh();
        }
    }

    private void DataTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_xmlMode || _edits is null || DataTree.SelectedItem is not TreeViewItem { Tag: RdlcFieldNode field })
            return;

        if (_selection?.Primary is { CanEditValue: true })
        {
            _edits.BindFieldToSelected(field.Name);
            SetStatus($"Bound Fields!{field.Name}.Value");
            return;
        }

        Point p = NextInsertPoint();
        string ds = string.Empty;
        if (DataTree.SelectedItem is TreeViewItem node &&
            node.Parent is TreeViewItem { Tag: RdlcDataSetNode parentDs })
            ds = parentDs.Name;

        IReportItem? created = _edits.InsertFieldTextbox(ds, field.Name, p.X, p.Y);
        if (created is not null)
            SetStatus($"Inserted field textbox '{created.Name}'");
    }

    private void AddParameter_Click(object sender, RoutedEventArgs e)
    {
        string name = NewParamName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;
        RdlcParameterNode? node = _schema?.AddParameter(name);
        if (node is not null)
        {
            NewParamName.Text = string.Empty;
            ParametersList.SelectedItem = _schema!.Parameters
                .FirstOrDefault(p => string.Equals(p.Name, node.Name, StringComparison.OrdinalIgnoreCase));
            SetStatus($"Added parameter '{node.Name}'");
        }
    }

    private void RemoveParameter_Click(object sender, RoutedEventArgs e)
    {
        if (ParametersList.SelectedItem is not RdlcParameterNode p || _schema is null)
            return;
        if (_schema.RemoveParameter(p.Name))
            SetStatus($"Removed parameter '{p.Name}'");
    }

    private void ApplyParameter_Click(object sender, RoutedEventArgs e)
    {
        if (ParametersList.SelectedItem is not RdlcParameterNode p || _schema is null)
            return;

        string dataType = (ParamTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "String";
        _schema.UpdateParameter(
            p.Name,
            dataType,
            ParamNullableBox.IsChecked == true,
            ParamAllowBlankBox.IsChecked == true,
            ParamPromptBox.Text,
            ParamDefaultBox.Text);
        SetStatus($"Updated parameter '{p.Name}'");
    }

    private void ParametersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ParametersList.SelectedItem is not RdlcParameterNode p)
            return;

        ParamPromptBox.Text = p.Prompt ?? p.Name;
        ParamDefaultBox.Text = p.DefaultValue ?? string.Empty;
        ParamNullableBox.IsChecked = p.Nullable;
        ParamAllowBlankBox.IsChecked = p.AllowBlank;
        ParamTypeBox.SelectedIndex = p.DataType switch
        {
            "Boolean" => 1,
            "DateTime" => 2,
            "Integer" => 3,
            "Float" => 4,
            _ => 0
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_xmlMode && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ApplyXmlNow();
            e.Handled = true;
            return;
        }

        if (!_xmlMode && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key is Key.OemPlus or Key.Add)
            {
                _surface?.ZoomIn();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                _surface?.ZoomOut();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                _surface?.ResetZoom();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Delete && !IsTextInputFocused())
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _toolbox?.SetActiveTool(ToolboxItemKind.Pointer);
            _selection?.Clear();
            e.Handled = true;
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => _surface?.ZoomIn();

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => _surface?.ZoomOut();

    private void ZoomReset_Click(object sender, RoutedEventArgs e) => _surface?.ResetZoom();

    private void OnSurfaceZoomChanged(object? sender, EventArgs e)
    {
        UpdateZoomLabel();
        SyncRulers();
    }

    private void UpdateZoomLabel()
    {
        double zoom = _surface?.Zoom ?? 1.0;
        ZoomPercentText.Text = string.Create(CultureInfo.InvariantCulture, $"{zoom * 100:0}%");
    }

    private void DesignScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => SyncRulers();

    private void SurfaceHost_SizeChanged(object sender, SizeChangedEventArgs e) => SyncRulers();

    private void DesignScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_xmlMode || _surface is null || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (e.Delta > 0)
            _surface.ZoomIn();
        else
            _surface.ZoomOut();

        e.Handled = true;
    }

    private void ApplyRulerVisibility()
    {
        Visibility vis = _showRulers ? Visibility.Visible : Visibility.Collapsed;
        RulerTopRow.Height = _showRulers ? new GridLength(22) : new GridLength(0);
        RulerLeftColumn.Width = _showRulers ? new GridLength(22) : new GridLength(0);
        RulerCorner.Visibility = vis;
        HorizontalRuler.Visibility = vis;
        VerticalRuler.Visibility = vis;
    }

    private void SyncRulers()
    {
        if (!_showRulers || _xmlMode || SurfaceHost.Content is null)
            return;

        try
        {
            double zoom = _surface?.Zoom ?? 1.0;
            HorizontalRuler.Zoom = zoom;
            VerticalRuler.Zoom = zoom;

            Point toH = SurfaceHost.TranslatePoint(new Point(0, 0), HorizontalRuler);
            Point toV = SurfaceHost.TranslatePoint(new Point(0, 0), VerticalRuler);
            HorizontalRuler.ContentOrigin = toH.X;
            VerticalRuler.ContentOrigin = toV.Y;
            HorizontalRuler.InvalidateVisual();
            VerticalRuler.InvalidateVisual();
        }
        catch (InvalidOperationException)
        {
            // Visual tree not ready yet (e.g. during unload).
        }
    }

    private static bool IsTextInputFocused() =>
        Keyboard.FocusedElement is TextBox or ComboBox or RichTextBox
        || Keyboard.FocusedElement?.GetType().Name is "TextArea" or "TextView";
}

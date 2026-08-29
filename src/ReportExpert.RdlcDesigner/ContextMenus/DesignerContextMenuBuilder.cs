using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Extensibility;

namespace ReportExpert.RdlcDesigner.ContextMenus;

/// <summary>
/// Builds context-sensitive menus for the design surface and report items.
/// </summary>
public sealed class DesignerContextMenuBuilder
{
    private readonly ISelectionService _selection;
    private readonly IDocumentEditService _edits;
    private readonly IPropertyService _properties;
    private readonly Func<ToolboxItemKind, Point, IReportItem?> _insertAt;
    private readonly Action? _openExpression;
    private readonly Action? _undo;
    private readonly Action? _redo;

    public DesignerContextMenuBuilder(
        ISelectionService selection,
        IDocumentEditService edits,
        IPropertyService properties,
        ISchemaService? schema,
        Func<ToolboxItemKind, Point, IReportItem?> insertAt,
        Action? openExpression = null,
        Action? undo = null,
        Action? redo = null)
    {
        _selection = selection;
        _edits = edits;
        _properties = properties;
        _ = schema;
        _insertAt = insertAt;
        _openExpression = openExpression;
        _undo = undo;
        _redo = redo;
    }

    public ContextMenu Build(Point insertPoint)
    {
        var menu = new ContextMenu();
        IReadOnlyList<IReportItem> selected = _selection.Selected;
        IReportItem? primary = _selection.Primary;

        if (selected.Count == 0)
            BuildSurfaceMenu(menu, insertPoint);
        else if (selected.Count > 1)
            BuildMultiSelectMenu(menu);
        else
            BuildItemMenu(menu, primary!, insertPoint);

        return menu;
    }

    private void BuildSurfaceMenu(ContextMenu menu, Point insertPoint)
    {
        menu.Items.Add(Header("Report"));
        AddInsert(menu, "Insert TextBox", ToolboxItemKind.TextBox, insertPoint);
        AddInsert(menu, "Insert Rectangle", ToolboxItemKind.Rectangle, insertPoint);
        AddInsert(menu, "Insert Line", ToolboxItemKind.Line, insertPoint);
        AddInsert(menu, "Insert Image", ToolboxItemKind.Image, insertPoint);
        menu.Items.Add(new Separator());
        AddCommand(menu, "Undo", () => _undo?.Invoke());
        AddCommand(menu, "Redo", () => _redo?.Invoke());
    }

    private void BuildMultiSelectMenu(ContextMenu menu)
    {
        menu.Items.Add(Header($"{_selection.Selected.Count} items"));
        AddCommand(menu, "Delete", DeleteSelection);
        menu.Items.Add(new Separator());
        AddCommand(menu, "Hide", () =>
        {
            foreach (IReportItem item in _selection.Selected)
                _properties.SetHidden(item, true);
        });
        AddCommand(menu, "Unhide", () =>
        {
            foreach (IReportItem item in _selection.Selected)
                _properties.SetHidden(item, false);
        });
    }

    private void BuildItemMenu(ContextMenu menu, IReportItem item, Point insertPoint)
    {
        menu.Items.Add(Header($"{item.Kind}: {item.Name}"));

        if (item.CanEditValue)
            AddCommand(menu, "Expression…", () => _openExpression?.Invoke());

        if (item.Kind == ReportItemKind.Image)
        {
            // Image-specific placeholders — properties pane handles source editing.
        }

        if (item.Kind is ReportItemKind.Rectangle)
        {
            var insertInside = new MenuItem { Header = "Insert inside" };
            AddInsert(insertInside, "TextBox", ToolboxItemKind.TextBox, insertPoint);
            AddInsert(insertInside, "Image", ToolboxItemKind.Image, insertPoint);
            AddInsert(insertInside, "Line", ToolboxItemKind.Line, insertPoint);
            menu.Items.Add(insertInside);
        }

        menu.Items.Add(new Separator());
        AddCommand(menu, item.Hidden ? "Unhide" : "Hide",
            () => _properties.SetHidden(item, !item.Hidden));

        if (item.Kind is not ReportItemKind.TablixCellTextbox)
            AddCommand(menu, "Delete", () => _edits.Delete(item));

        menu.Items.Add(new Separator());
        AddCommand(menu, "Select none", () => _selection.Clear());
    }

    private void DeleteSelection()
    {
        foreach (IReportItem item in _selection.Selected.ToList())
            _edits.Delete(item);
    }

    private void AddInsert(ItemsControl parent, string header, ToolboxItemKind kind, Point point)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => _insertAt(kind, point);
        parent.Items.Add(item);
    }

    private static void AddCommand(ItemsControl parent, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        parent.Items.Add(item);
    }

    private static MenuItem Header(string text) =>
        new() { Header = text, IsEnabled = false, FontWeight = FontWeights.SemiBold };
}

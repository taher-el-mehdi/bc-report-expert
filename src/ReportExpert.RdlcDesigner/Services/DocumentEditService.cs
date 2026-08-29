using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Extensibility;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Services;

public sealed class DocumentEditService : IDocumentEditService
{
    private readonly Func<RdlcDocument?> _document;
    private readonly ISelectionService _selection;
    private readonly IPropertyService _properties;
    private readonly IUndoService _undo;
    private readonly Action? _afterChange;

    public DocumentEditService(
        Func<RdlcDocument?> document,
        ISelectionService selection,
        IPropertyService properties,
        IUndoService undo,
        Action? afterChange = null)
    {
        _document = document;
        _selection = selection;
        _properties = properties;
        _undo = undo;
        _afterChange = afterChange;
    }

    public IReportItem? Insert(ToolboxItemKind kind, double leftPx, double topPx)
    {
        if (kind == ToolboxItemKind.Pointer)
            return null;

        RdlcDocument? doc = _document();
        if (doc is null)
            return null;

        XElement element = kind switch
        {
            ToolboxItemKind.TextBox => CreateTextbox(doc, "Textbox", "Textbox", leftPx, topPx, 144, 24),
            ToolboxItemKind.Rectangle => CreateRectangle(doc, leftPx, topPx),
            ToolboxItemKind.Line => CreateLine(doc, leftPx, topPx),
            ToolboxItemKind.Image => CreateImage(doc, leftPx, topPx),
            ToolboxItemKind.Table => CreateTablix(doc, leftPx, topPx, TablixLayout.Table),
            ToolboxItemKind.Matrix => CreateTablix(doc, leftPx, topPx, TablixLayout.Matrix),
            ToolboxItemKind.List => CreateTablix(doc, leftPx, topPx, TablixLayout.List),
            ToolboxItemKind.Chart => CreateChart(doc, leftPx, topPx),
            ToolboxItemKind.Subreport => CreateSubreport(doc, leftPx, topPx),
            ToolboxItemKind.Gauge => CreateGauge(doc, leftPx, topPx),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var cmd = new InsertItemCommand(doc, element, After);
        _undo.Execute(cmd);
        ReportItemModel? model = doc.FindModel(element.Attribute("Name")?.Value ?? string.Empty);
        if (model is not null)
            _selection.Select(model);
        return model;
    }

    public IReportItem? InsertFieldTextbox(string datasetName, string fieldName, double leftPx, double topPx)
    {
        RdlcDocument? doc = _document();
        if (doc is null || string.IsNullOrWhiteSpace(fieldName))
            return null;

        string expr = $"=Fields!{fieldName}.Value";
        XElement element = CreateTextbox(doc, SanitizeName(fieldName), expr, leftPx, topPx, 144, 24);
        var cmd = new InsertItemCommand(doc, element, After);
        _undo.Execute(cmd);
        ReportItemModel? model = doc.FindModel(element.Attribute("Name")?.Value ?? string.Empty);
        if (model is not null)
            _selection.Select(model);
        return model;
    }

    public void BindFieldToSelected(string fieldName)
    {
        if (_selection.Primary is not { CanEditValue: true } item)
            return;
        if (string.IsNullOrWhiteSpace(fieldName))
            return;
        _properties.SetValue(item, $"=Fields!{fieldName}.Value");
    }

    public bool Delete(IReportItem item)
    {
        if (item is not ReportItemModel model)
            return false;
        if (model.Kind is ReportItemKind.TablixCellTextbox)
            return false;

        RdlcDocument? doc = _document();
        if (doc is null)
            return false;

        _undo.Execute(new DeleteItemCommand(doc, model.Element, After));
        if (_selection.Selected.Any(s => ReferenceEquals(s, item) || string.Equals(s.Id, item.Id, StringComparison.Ordinal)))
        {
            var remaining = _selection.Selected
                .Where(s => !ReferenceEquals(s, item) && !string.Equals(s.Id, item.Id, StringComparison.Ordinal))
                .ToList();
            if (remaining.Count == 0)
                _selection.Clear();
            else
                _selection.SelectMany(remaining);
        }

        return true;
    }

    private void After()
    {
        _document()?.NotifyChanged();
        _afterChange?.Invoke();
    }

    private static string SanitizeName(string name)
    {
        char[] chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        if (chars.Length == 0 || char.IsDigit(chars[0]))
            return "Field_" + name;
        return new string(chars);
    }

    private static XElement CreateTextbox(
        RdlcDocument doc,
        string preferredName,
        string value,
        double left,
        double top,
        double width,
        double height)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName(preferredName);
        return new XElement(ns + "Textbox",
            new XAttribute("Name", name),
            new XElement(ns + "CanGrow", "true"),
            new XElement(ns + "KeepTogether", "true"),
            new XElement(ns + "Paragraphs",
                new XElement(ns + "Paragraph",
                    new XElement(ns + "TextRuns",
                        new XElement(ns + "TextRun",
                            new XElement(ns + "Value", value),
                            new XElement(ns + "Style"))),
                    new XElement(ns + "Style"))),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", RdlUnits.FromPx(height)),
            new XElement(ns + "Width", RdlUnits.FromPx(width)),
            new XElement(ns + "Style",
                new XElement(ns + "Border", new XElement(ns + "Style", "None")),
                new XElement(ns + "PaddingLeft", "2pt"),
                new XElement(ns + "PaddingRight", "2pt"),
                new XElement(ns + "PaddingTop", "2pt"),
                new XElement(ns + "PaddingBottom", "2pt")));
    }

    private static XElement CreateRectangle(RdlcDocument doc, double left, double top)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName("Rectangle");
        return new XElement(ns + "Rectangle",
            new XAttribute("Name", name),
            new XElement(ns + "KeepTogether", "true"),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", RdlUnits.FromPx(96)),
            new XElement(ns + "Width", RdlUnits.FromPx(192)),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"),
                    new XElement(ns + "Width", "1pt"))));
    }

    private static XElement CreateLine(RdlcDocument doc, double left, double top)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName("Line");
        return new XElement(ns + "Line",
            new XAttribute("Name", name),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", "0in"),
            new XElement(ns + "Width", RdlUnits.FromPx(192)),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Width", "1pt"),
                    new XElement(ns + "Color", "Black"))));
    }

    private static XElement CreateImage(RdlcDocument doc, double left, double top)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName("Image");
        return new XElement(ns + "Image",
            new XAttribute("Name", name),
            new XElement(ns + "Source", "External"),
            new XElement(ns + "Value", ""),
            new XElement(ns + "Sizing", "FitProportional"),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", RdlUnits.FromPx(96)),
            new XElement(ns + "Width", RdlUnits.FromPx(144)),
            new XElement(ns + "Style",
                new XElement(ns + "Border", new XElement(ns + "Style", "Solid"))));
    }

    private enum TablixLayout { Table, Matrix, List }

    private static XElement CreateTablix(RdlcDocument doc, double left, double top, TablixLayout layout)
    {
        XNamespace ns = doc.Namespace;
        string preferred = layout switch
        {
            TablixLayout.Matrix => "Matrix",
            TablixLayout.List => "List",
            _ => "Tablix"
        };
        string name = doc.AllocateUniqueName(preferred);

        return layout switch
        {
            TablixLayout.Matrix => CreateMatrixTablix(ns, name, left, top),
            TablixLayout.List => CreateListTablix(ns, name, left, top),
            _ => CreateTableTablix(ns, name, left, top)
        };
    }

    private static XElement CreateTableTablix(XNamespace ns, string name, double left, double top)
    {
        string c1 = name + "_Header1";
        string c2 = name + "_Header2";
        string d1 = name + "_Value1";
        string d2 = name + "_Value2";

        return new XElement(ns + "Tablix",
            new XAttribute("Name", name),
            new XElement(ns + "TablixBody",
                new XElement(ns + "TablixColumns",
                    new XElement(ns + "TablixColumn", new XElement(ns + "Width", "1.5in")),
                    new XElement(ns + "TablixColumn", new XElement(ns + "Width", "1.5in"))),
                new XElement(ns + "TablixRows",
                    CreateTablixRow(ns, "0.25in", CreateCellTextbox(ns, c1, "Column1"), CreateCellTextbox(ns, c2, "Column2")),
                    CreateTablixRow(ns, "0.25in", CreateCellTextbox(ns, d1, ""), CreateCellTextbox(ns, d2, "")))),
            new XElement(ns + "TablixColumnHierarchy",
                new XElement(ns + "TablixMembers",
                    new XElement(ns + "TablixMember"),
                    new XElement(ns + "TablixMember"))),
            new XElement(ns + "TablixRowHierarchy",
                new XElement(ns + "TablixMembers",
                    new XElement(ns + "TablixMember"),
                    new XElement(ns + "TablixMember"))),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", "0.5in"),
            new XElement(ns + "Width", "3in"),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"))));
    }

    private static XElement CreateMatrixTablix(XNamespace ns, string name, double left, double top)
    {
        string corner = name + "_Corner";
        string colHdr = name + "_ColHeader";
        string rowHdr = name + "_RowHeader";
        string cell = name + "_Cell";

        return new XElement(ns + "Tablix",
            new XAttribute("Name", name),
            new XElement(ns + "TablixBody",
                new XElement(ns + "TablixColumns",
                    new XElement(ns + "TablixColumn", new XElement(ns + "Width", "1.25in"))),
                new XElement(ns + "TablixRows",
                    CreateTablixRow(ns, "0.25in", CreateCellTextbox(ns, cell, "")))),
            new XElement(ns + "TablixColumnHierarchy",
                new XElement(ns + "TablixMembers",
                    new XElement(ns + "TablixMember",
                        new XElement(ns + "Group", new XAttribute("Name", name + "_ColGroup"),
                            new XElement(ns + "GroupExpressions",
                                new XElement(ns + "GroupExpression"))),
                        new XElement(ns + "TablixHeader",
                            new XElement(ns + "Size", "1in"),
                            new XElement(ns + "CellContents", CreateStandaloneTextbox(ns, colHdr, "ColumnGroup")))))),
            new XElement(ns + "TablixRowHierarchy",
                new XElement(ns + "TablixMembers",
                    new XElement(ns + "TablixMember",
                        new XElement(ns + "Group", new XAttribute("Name", name + "_RowGroup"),
                            new XElement(ns + "GroupExpressions",
                                new XElement(ns + "GroupExpression"))),
                        new XElement(ns + "TablixHeader",
                            new XElement(ns + "Size", "1in"),
                            new XElement(ns + "CellContents", CreateStandaloneTextbox(ns, rowHdr, "RowGroup")))))),
            new XElement(ns + "TablixCorner",
                new XElement(ns + "TablixCornerRows",
                    new XElement(ns + "TablixCornerRow",
                        new XElement(ns + "TablixCornerCell",
                            new XElement(ns + "CellContents", CreateStandaloneTextbox(ns, corner, "")))))),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", "0.5in"),
            new XElement(ns + "Width", "2.25in"),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"))));
    }

    private static XElement CreateListTablix(XNamespace ns, string name, double left, double top)
    {
        string cellBox = name + "_Content";
        string rectName = name + "_Rect";

        var rectangle = new XElement(ns + "Rectangle",
            new XAttribute("Name", rectName),
            new XElement(ns + "ReportItems",
                CreateStandaloneTextbox(ns, cellBox, "")),
            new XElement(ns + "KeepTogether", "true"),
            new XElement(ns + "Style",
                new XElement(ns + "Border", new XElement(ns + "Style", "None"))));

        return new XElement(ns + "Tablix",
            new XAttribute("Name", name),
            new XElement(ns + "TablixBody",
                new XElement(ns + "TablixColumns",
                    new XElement(ns + "TablixColumn", new XElement(ns + "Width", "3in"))),
                new XElement(ns + "TablixRows",
                    new XElement(ns + "TablixRow",
                        new XElement(ns + "Height", "0.75in"),
                        new XElement(ns + "TablixCells",
                            new XElement(ns + "TablixCell",
                                new XElement(ns + "CellContents", rectangle)))))),
            new XElement(ns + "TablixColumnHierarchy",
                new XElement(ns + "TablixMembers", new XElement(ns + "TablixMember"))),
            new XElement(ns + "TablixRowHierarchy",
                new XElement(ns + "TablixMembers",
                    new XElement(ns + "TablixMember",
                        new XElement(ns + "Group", new XAttribute("Name", name + "_Details"))))),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", "0.75in"),
            new XElement(ns + "Width", "3in"),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"))));
    }

    private static XElement CreateTablixRow(XNamespace ns, string height, params XElement[] cells) =>
        new XElement(ns + "TablixRow",
            new XElement(ns + "Height", height),
            new XElement(ns + "TablixCells", cells.Select(c =>
                new XElement(ns + "TablixCell", new XElement(ns + "CellContents", c)))));

    private static XElement CreateCellTextbox(XNamespace ns, string name, string value) =>
        CreateStandaloneTextbox(ns, name, value);

    private static XElement CreateStandaloneTextbox(XNamespace ns, string name, string value) =>
        new XElement(ns + "Textbox",
            new XAttribute("Name", name),
            new XElement(ns + "CanGrow", "true"),
            new XElement(ns + "KeepTogether", "true"),
            new XElement(ns + "Paragraphs",
                new XElement(ns + "Paragraph",
                    new XElement(ns + "TextRuns",
                        new XElement(ns + "TextRun",
                            new XElement(ns + "Value", value),
                            new XElement(ns + "Style"))),
                    new XElement(ns + "Style"))),
            new XElement(ns + "Style",
                new XElement(ns + "Border", new XElement(ns + "Style", "None")),
                new XElement(ns + "PaddingLeft", "2pt"),
                new XElement(ns + "PaddingRight", "2pt"),
                new XElement(ns + "PaddingTop", "2pt"),
                new XElement(ns + "PaddingBottom", "2pt")));

    private static XElement CreateChart(RdlcDocument doc, double left, double top)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName("Chart");
        return new XElement(ns + "Chart",
            new XAttribute("Name", name),
            new XElement(ns + "ChartCategoryHierarchy",
                new XElement(ns + "ChartMembers", new XElement(ns + "ChartMember"))),
            new XElement(ns + "ChartSeriesHierarchy",
                new XElement(ns + "ChartMembers", new XElement(ns + "ChartMember"))),
            new XElement(ns + "ChartData",
                new XElement(ns + "ChartSeriesCollection",
                    new XElement(ns + "ChartSeries", new XAttribute("Name", "Series1"),
                        new XElement(ns + "ChartDataPoints",
                            new XElement(ns + "ChartDataPoint",
                                new XElement(ns + "ChartDataPointValues",
                                    new XElement(ns + "Y", "1")),
                                new XElement(ns + "ChartDataLabel",
                                    new XElement(ns + "Style")),
                                new XElement(ns + "Style"),
                                new XElement(ns + "ChartMarker",
                                    new XElement(ns + "Style")),
                                new XElement(ns + "DataElementOutput", "Output"))),
                        new XElement(ns + "Type", "Column"),
                        new XElement(ns + "Style"),
                        new XElement(ns + "ChartEmptyPoints",
                            new XElement(ns + "Style"),
                            new XElement(ns + "ChartMarker", new XElement(ns + "Style")),
                            new XElement(ns + "ChartDataLabel", new XElement(ns + "Style"))),
                        new XElement(ns + "ValueAxisName", "Primary"),
                        new XElement(ns + "CategoryAxisName", "Primary"),
                        new XElement(ns + "ChartSmartLabel",
                            new XElement(ns + "CalloutLineColor", "Black"),
                            new XElement(ns + "TextOrientation", "Auto"),
                            new XElement(ns + "Disabled", "false"))))),
            new XElement(ns + "ChartAreas",
                new XElement(ns + "ChartArea", new XAttribute("Name", "Default"),
                    new XElement(ns + "ChartCategoryAxes",
                        new XElement(ns + "ChartAxis", new XAttribute("Name", "Primary"),
                            new XElement(ns + "Style"),
                            new XElement(ns + "ChartAxisTitle",
                                new XElement(ns + "Caption", ""),
                                new XElement(ns + "Style")),
                            new XElement(ns + "ChartMajorGridLines", new XElement(ns + "Enabled", "false"), new XElement(ns + "Style")),
                            new XElement(ns + "ChartMinorGridLines", new XElement(ns + "Enabled", "false"), new XElement(ns + "Style")),
                            new XElement(ns + "ChartMajorTickMarks", new XElement(ns + "Style")),
                            new XElement(ns + "ChartMinorTickMarks", new XElement(ns + "Style")))),
                    new XElement(ns + "ChartValueAxes",
                        new XElement(ns + "ChartAxis", new XAttribute("Name", "Primary"),
                            new XElement(ns + "Style"),
                            new XElement(ns + "ChartAxisTitle",
                                new XElement(ns + "Caption", ""),
                                new XElement(ns + "Style")),
                            new XElement(ns + "ChartMajorGridLines", new XElement(ns + "Style")),
                            new XElement(ns + "ChartMinorGridLines", new XElement(ns + "Enabled", "false"), new XElement(ns + "Style")),
                            new XElement(ns + "ChartMajorTickMarks", new XElement(ns + "Style")),
                            new XElement(ns + "ChartMinorTickMarks", new XElement(ns + "Style")))),
                    new XElement(ns + "Style"))),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", RdlUnits.FromPx(192)),
            new XElement(ns + "Width", RdlUnits.FromPx(288)),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"),
                    new XElement(ns + "Width", "1pt")),
                new XElement(ns + "BackgroundColor", "White")));
    }

    private static XElement CreateSubreport(RdlcDocument doc, double left, double top)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName("Subreport");
        return new XElement(ns + "Subreport",
            new XAttribute("Name", name),
            new XElement(ns + "ReportName", ""),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", RdlUnits.FromPx(96)),
            new XElement(ns + "Width", RdlUnits.FromPx(288)),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"))));
    }

    private static XElement CreateGauge(RdlcDocument doc, double left, double top)
    {
        XNamespace ns = doc.Namespace;
        string name = doc.AllocateUniqueName("GaugePanel");
        return new XElement(ns + "GaugePanel",
            new XAttribute("Name", name),
            new XElement(ns + "Top", RdlUnits.FromPx(top)),
            new XElement(ns + "Left", RdlUnits.FromPx(left)),
            new XElement(ns + "Height", RdlUnits.FromPx(144)),
            new XElement(ns + "Width", RdlUnits.FromPx(144)),
            new XElement(ns + "Style",
                new XElement(ns + "Border",
                    new XElement(ns + "Style", "Solid"),
                    new XElement(ns + "Color", "LightGrey"),
                    new XElement(ns + "Width", "1pt")),
                new XElement(ns + "BackgroundColor", "White")));
    }
}

public sealed class InsertItemCommand : IDesignerCommand
{
    private readonly RdlcDocument _doc;
    private readonly XElement _element;
    private readonly Action? _after;
    private XElement? _parent;

    public InsertItemCommand(RdlcDocument doc, XElement element, Action? after)
    {
        _doc = doc;
        _element = element;
        _after = after;
    }

    public string Name => "Insert item";

    public void Execute()
    {
        _parent ??= _doc.GetOrCreateBodyReportItems();
        if (_element.Parent is null)
            _parent.Add(_element);
        _doc.RebuildIndex();
        _after?.Invoke();
    }

    public void Unexecute()
    {
        _element.Remove();
        _doc.RebuildIndex();
        _after?.Invoke();
    }
}

public sealed class DeleteItemCommand : IDesignerCommand
{
    private readonly RdlcDocument _doc;
    private readonly XElement _element;
    private readonly Action? _after;
    private XElement? _parent;
    private XNode? _next;

    public DeleteItemCommand(RdlcDocument doc, XElement element, Action? after)
    {
        _doc = doc;
        _element = element;
        _after = after;
    }

    public string Name => "Delete item";

    public void Execute()
    {
        _parent = _element.Parent as XElement;
        _next = _element.NextNode;
        _element.Remove();
        _doc.RebuildIndex();
        _after?.Invoke();
    }

    public void Unexecute()
    {
        if (_parent is null)
            return;
        if (_next is not null)
            _next.AddBeforeSelf(_element);
        else
            _parent.Add(_element);
        _doc.RebuildIndex();
        _after?.Invoke();
    }
}

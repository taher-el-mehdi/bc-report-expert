using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Model;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfGrid = System.Windows.Controls.Grid;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace ReportExpert.RdlcDesigner.Surface;

/// <summary>
/// Builds a classic Report Builder–style design surface from RDLC XML
/// (dashed item outlines, monochrome field markers, sharp grid).
/// </summary>
public sealed class RdlcVisualRenderer
{
    private static readonly SolidColorBrush Ink = Freeze(0x00, 0x00, 0x00);
    private static readonly SolidColorBrush PageBackground = Freeze(0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush GridLine = Freeze(0x00, 0x00, 0x00);
    private static readonly SolidColorBrush MutedInk = Freeze(0x66, 0x66, 0x66);
    private static readonly SolidColorBrush OutlineStroke = Freeze(0x2F, 0x6F, 0xB5);
    private static readonly DoubleCollection DashPattern = [3, 2];
    private static readonly Regex FieldsRegex = new(@"Fields!(\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Minimum readable textbox height on the design surface (px).</summary>
    private const double MinReadableHeight = 22;

    /// <summary>Cap RDL font sizes so design labels stay compact and readable.</summary>
    private const double MaxDesignFontSize = 11;

    private int _itemCount;
    private int _tablixCount;
    private RdlcDocument? _document;
    private readonly Dictionary<string, FrameworkElement> _visuals = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, FrameworkElement> Visuals => _visuals;

    public (FrameworkElement Element, string Summary) Build(string path) =>
        Build(XDocument.Load(path), document: null);

    public (FrameworkElement Element, string Summary) Build(RdlcDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Build(document.Xml, document);
    }

    public (FrameworkElement Element, string Summary) Build(XDocument doc, RdlcDocument? document)
    {
        _itemCount = 0;
        _tablixCount = 0;
        _document = document;
        _visuals.Clear();

        XElement? report = doc.Root;
        if (report is null || report.Name.LocalName != "Report")
            throw new InvalidOperationException("Invalid RDLC: no Report element.");

        XNamespace ns = report.Name.Namespace;
        (XElement? body, XElement? page) = ResolveBodyAndPage(report, ns);
        if (body is null)
            throw new InvalidOperationException("Invalid RDLC: no Body element.");

        double pageWidth = ToPx(Value(page, ns, "PageWidth") ?? Value(report, ns, "PageWidth") ?? "8.5in");
        if (pageWidth <= 0)
            pageWidth = 8.5 * 96;

        var pagePanel = new StackPanel
        {
            Background = PageBackground,
            Width = pageWidth,
            MinHeight = 200
        };

        // Page header
        XElement? pageHeader = page?.Element(ns + "PageHeader");
        if (pageHeader is not null)
            pagePanel.Children.Add(BuildSection(pageHeader, ns, isFooter: false));

        // Body
        var bodySection = BuildSection(body, ns, isFooter: false);
        string? leftMargin = Value(page, ns, "LeftMargin") ?? Value(report, ns, "LeftMargin");
        string? rightMargin = Value(page, ns, "RightMargin") ?? Value(report, ns, "RightMargin");
        string? topMargin = Value(page, ns, "TopMargin") ?? Value(report, ns, "TopMargin");
        bodySection.Padding = new Thickness(
            ToPx(leftMargin),
            ToPx(topMargin),
            ToPx(rightMargin),
            0);
        pagePanel.Children.Add(bodySection);

        // Page footer
        XElement? pageFooter = page?.Element(ns + "PageFooter");
        if (pageFooter is not null)
            pagePanel.Children.Add(BuildSection(pageFooter, ns, isFooter: true));

        // Classic Report Builder: white page, solid black hairline, no shadow / radius
        var pageFrame = new Border
        {
            Background = PageBackground,
            BorderBrush = Ink,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
            Child = pagePanel,
            Width = pageWidth,
            SnapsToDevicePixels = true,
            Tag = pageWidth // used by HomeView for fit-to-width scaling
        };

        string summary = $"{_itemCount} item(s) · {_tablixCount} tablix(es) · {pageWidth / 96.0:0.##}in wide";
        return (pageFrame, summary);
    }

    private Border BuildSection(XElement section, XNamespace ns, bool isFooter)
    {
        double minHeight = ToPx(Value(section, ns, "Height"));
        var canvas = new Canvas
        {
            MinHeight = Math.Max(minHeight, 24),
            ClipToBounds = false,
            Background = PageBackground
        };

        XElement? items = section.Element(ns + "ReportItems");
        double maxBottom = minHeight;
        if (items is not null)
        {
            foreach (XElement child in items.Elements())
            {
                UIElement? el = BuildItem(child, ns, positioned: true, out double bottom);
                if (el is null)
                    continue;
                canvas.Children.Add(el);
                maxBottom = Math.Max(maxBottom, bottom);
            }
        }

        canvas.Height = Math.Max(canvas.MinHeight, maxBottom + 8);

        var border = new Border
        {
            Child = canvas,
            Background = PageBackground,
            BorderBrush = Ink,
            BorderThickness = isFooter ? new Thickness(0, 1, 0, 0) : new Thickness(0),
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            ClipToBounds = false,
            // Match canvas so the white page grows with overflowing tablixes.
            MinHeight = canvas.Height
        };

        if (isFooter)
            border.BorderBrush = Ink;

        return border;
    }

    private UIElement? BuildItem(XElement item, XNamespace ns, bool positioned, out double bottom)
    {
        bottom = 0;
        if (IsHidden(item, ns))
            return null;

        _itemCount++;
        string type = item.Name.LocalName;
        double left = ToPx(Value(item, ns, "Left"));
        double top = ToPx(Value(item, ns, "Top"));
        double width = ToPx(Value(item, ns, "Width"));
        double height = ToPx(Value(item, ns, "Height"));

        FrameworkElement? content = type switch
        {
            "Textbox" => BuildTextbox(item, ns, width, height),
            "Rectangle" => BuildRectangle(item, ns, width, height),
            "Tablix" => BuildTablix(item, ns, width, height),
            "Image" => BuildImage(item, ns, width, height),
            "Line" => BuildLine(width, height),
            "Chart" => BuildUnsupported($"Chart · {Attr(item, "Name") ?? "Chart"}", width, height),
            "Gauge" or "GaugePanel" => BuildUnsupported($"Gauge · {Attr(item, "Name") ?? "Gauge"}", width, height),
            "Subreport" => BuildUnsupported(
                $"Subreport · {Value(item, ns, "ReportName") ?? Attr(item, "Name") ?? "Subreport"}",
                width,
                height),
            "Map" => BuildUnsupported($"Map · {Attr(item, "Name") ?? "Map"}", width, height),
            _ => BuildGenericContainer(item, ns, width, height)
        };

        if (content is null)
            return null;

        // Rectangles keep exact RDL geometry for nested absolute layout.
        // Text/image frames may grow vertically so labels stay readable.
        if (type is "Textbox" or "Image" or "Chart" or "Gauge" or "GaugePanel" or "Subreport" or "Map")
            content = DashedFrame(content, width, height, FrameKind.Content);
        else if (type is "Rectangle")
            content = DashedFrame(content, width, height, FrameKind.Container);
        else if (type is "Line")
        {
            // Give lines a hittable band for selection / move.
            content = DashedFrame(content, width, Math.Max(height, 8), FrameKind.Content);
        }

        TagDesignItem(content, item);

        if (positioned)
        {
            Canvas.SetLeft(content, left);
            Canvas.SetTop(content, top);
            // Tablixes/containers often grow past the RDL Height (auto rows, readable labels).
            // Measure the real visual height so the body canvas expands and nothing is cropped.
            double measuredH = MeasureContentHeight(content, width, height);
            bottom = top + measuredH;
        }
        else
        {
            bottom = MeasureContentHeight(content, width, height);
        }

        return content;
    }

    private void TagDesignItem(FrameworkElement visual, XElement item)
    {
        if (_document is null)
            return;

        ReportItemModel? model = _document.MutableItems.FirstOrDefault(i => ReferenceEquals(i.Element, item));
        if (model is null)
            return;

        DesignElement.SetItemId(visual, model.Id);
        _visuals[model.Id] = visual;
    }

    /// <summary>
    /// Returns the taller of the RDL height and the element's desired height after measure.
    /// Ensures design-surface sections grow to fit tablixes instead of clipping them.
    /// </summary>
    private static double MeasureContentHeight(FrameworkElement content, double width, double rdlHeight)
    {
        double constraintW = width > 0 ? width : double.PositiveInfinity;
        content.Measure(new System.Windows.Size(constraintW, double.PositiveInfinity));
        double desired = content.DesiredSize.Height;
        double floor = rdlHeight > 0 ? rdlHeight : MinReadableHeight;
        return Math.Max(floor, desired);
    }

    private FrameworkElement BuildTextbox(XElement item, XNamespace ns, double width, double height)
    {
        string tip = CollectTextboxPlainText(item, ns);
        double fontSize = height > 0 && height < 18 ? 9 : 10;
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = fontSize,
            LineHeight = fontSize + 2,
            Foreground = Ink,
            ToolTip = string.IsNullOrWhiteSpace(tip) ? null : tip
        };
        FillTextboxInlines(block.Inlines, item, ns);

        return new Border
        {
            Child = block,
            Background = PageBackground,
            CornerRadius = new CornerRadius(0),
            ClipToBounds = true,
            SnapsToDevicePixels = true,
            ToolTip = string.IsNullOrWhiteSpace(tip) ? null : tip
        };
    }

    private static string CollectTextboxPlainText(XElement textbox, XNamespace ns)
    {
        var parts = new List<string>();
        XElement? paragraphs = textbox.Element(ns + "Paragraphs");
        if (paragraphs is null)
        {
            string? simple = Value(textbox, ns, "Value");
            return FormatExpressionPlain(simple);
        }

        foreach (XElement para in paragraphs.Elements(ns + "Paragraph"))
        {
            XElement? runs = para.Element(ns + "TextRuns");
            if (runs is null)
                continue;
            foreach (XElement run in runs.Elements(ns + "TextRun"))
                parts.Add(FormatExpressionPlain(Value(run, ns, "Value")));
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string FormatExpressionPlain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (!value.StartsWith('='))
            return value;

        var fields = FieldsRegex.Matches(value)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(f => $"«{f}»");
        string joined = string.Join(" ", fields);
        return string.IsNullOrWhiteSpace(joined) ? value : joined;
    }

    private void FillTextboxInlines(InlineCollection inlines, XElement textbox, XNamespace ns)
    {
        XElement? paragraphs = textbox.Element(ns + "Paragraphs");
        if (paragraphs is null)
        {
            string? simple = Value(textbox, ns, "Value");
            if (!string.IsNullOrEmpty(simple))
                AppendExpression(inlines, simple);
            return;
        }

        bool firstPara = true;
        foreach (XElement para in paragraphs.Elements(ns + "Paragraph"))
        {
            if (!firstPara)
                inlines.Add(new LineBreak());
            firstPara = false;

            XElement? runs = para.Element(ns + "TextRuns");
            if (runs is null)
                continue;

            foreach (XElement run in runs.Elements(ns + "TextRun"))
            {
                string? value = Value(run, ns, "Value");
                if (string.IsNullOrEmpty(value))
                    continue;

                AppendExpression(inlines, value, run.Element(ns + "Style"), ns);
            }
        }
    }

    private static void AppendExpression(
        InlineCollection inlines,
        string value,
        XElement? style = null,
        XNamespace? ns = null)
    {
        if (!value.StartsWith('='))
        {
            inlines.Add(CreateStyledRun(value, style, ns));
            return;
        }

        var fields = FieldsRegex.Matches(value)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fields.Count > 0)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                    inlines.Add(new Run(" "));
                inlines.Add(CreateFieldTag(fields[i]));
            }
            return;
        }

        string simplified = value[1..]
            .Replace("Today()", DateTime.Now.ToShortDateString(), StringComparison.OrdinalIgnoreCase)
            .Replace("Now()", DateTime.Now.ToString("g"), StringComparison.OrdinalIgnoreCase)
            .Replace("Globals!PageNumber", "1", StringComparison.OrdinalIgnoreCase)
            .Replace("Globals!TotalPages", "1", StringComparison.OrdinalIgnoreCase)
            .Replace("Globals!ReportName", "Report", StringComparison.OrdinalIgnoreCase)
            .Replace("User!UserID", "User", StringComparison.OrdinalIgnoreCase);

        inlines.Add(CreateStyledRun(simplified.Trim().Length > 0 ? simplified.Trim() : value, style, ns));
    }

    private static Inline CreateFieldTag(string fieldName)
    {
        // Plain text marker — wraps cleanly inside cells (no nested chrome box).
        // FontSize inherits from the hosting TextBlock (9–10 on the design surface).
        return new Run($"«{fieldName}»")
        {
            FontFamily = new WpfFontFamily("Consolas"),
            Foreground = Ink,
            FontWeight = FontWeights.Normal
        };
    }

    private static Run CreateStyledRun(string text, XElement? style, XNamespace? ns)
    {
        var run = new Run(text) { Foreground = Ink };
        if (style is null || ns is null)
            return run;

        string? font = Value(style, ns, "FontFamily");
        if (!string.IsNullOrWhiteSpace(font) && !font.StartsWith('='))
            run.FontFamily = new WpfFontFamily(font);

        string? size = Value(style, ns, "FontSize");
        if (!string.IsNullOrWhiteSpace(size) && !size.StartsWith('=') &&
            double.TryParse(size.Replace("pt", "", StringComparison.OrdinalIgnoreCase).Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double pt) && pt > 0)
            run.FontSize = Math.Min(pt * 96.0 / 72.0, MaxDesignFontSize);

        string? weight = Value(style, ns, "FontWeight");
        if (!string.IsNullOrWhiteSpace(weight) &&
            weight is "Bold" or "SemiBold" or "Medium")
            run.FontWeight = FontWeights.Bold;

        string? color = Value(style, ns, "Color");
        if (!string.IsNullOrWhiteSpace(color) &&
            !color.StartsWith('=') &&
            !color.Contains("System.Windows.", StringComparison.Ordinal) &&
            TryParseColor(color, out WpfColor parsed))
            run.Foreground = new SolidColorBrush(parsed);

        return run;
    }

    private FrameworkElement BuildRectangle(XElement item, XNamespace ns, double width, double height)
    {
        XElement? style = item.Element(ns + "Style");
        string? bg = Value(style, ns, "BackgroundColor");
        Brush fill = PageBackground;
        if (!string.IsNullOrWhiteSpace(bg) &&
            !bg.StartsWith('=') &&
            TryParseColor(bg, out WpfColor bgColor))
            fill = new SolidColorBrush(bgColor);

        var canvas = new Canvas
        {
            MinWidth = width > 0 ? width : 40,
            MinHeight = height > 0 ? height : 24,
            ClipToBounds = false,
            Background = fill
        };
        if (width > 0) canvas.Width = width;
        if (height > 0) canvas.Height = height;

        // Draw RDL border (preview of printed border) under the design-time dashed chrome.
        XElement? border = style?.Element(ns + "Border");
        string? borderStyle = Value(border, ns, "Style");
        if (!string.IsNullOrWhiteSpace(borderStyle) &&
            !borderStyle.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            string? borderColor = Value(border, ns, "Color") ?? "Black";
            Brush stroke = Ink;
            if (TryParseColor(borderColor, out WpfColor bc))
                stroke = new SolidColorBrush(bc);

            double thickness = 1;
            string? widthRaw = Value(border, ns, "Width");
            if (!string.IsNullOrWhiteSpace(widthRaw))
            {
                double px = ToPx(widthRaw);
                if (px > 0)
                    thickness = Math.Max(1, px);
            }

            var rdlBorder = new WpfRectangle
            {
                Width = width > 0 ? width : 40,
                Height = height > 0 ? height : 24,
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = WpfBrushes.Transparent,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
            if (borderStyle.Equals("Dashed", StringComparison.OrdinalIgnoreCase))
                rdlBorder.StrokeDashArray = [4, 2];
            else if (borderStyle.Equals("Dotted", StringComparison.OrdinalIgnoreCase))
                rdlBorder.StrokeDashArray = [1, 2];

            Canvas.SetLeft(rdlBorder, 0);
            Canvas.SetTop(rdlBorder, 0);
            canvas.Children.Add(rdlBorder);
        }

        XElement? items = item.Element(ns + "ReportItems");
        double maxBottom = height > 0 ? height : 24;
        if (items is not null)
        {
            foreach (XElement child in items.Elements())
            {
                UIElement? el = BuildItem(child, ns, positioned: true, out double bottom);
                if (el is null)
                    continue;
                canvas.Children.Add(el);
                maxBottom = Math.Max(maxBottom, bottom);
            }
        }

        // Always grow to fit nested items — RDL Height is a floor, not a clip.
        canvas.Height = Math.Max(24, maxBottom + 4);
        canvas.MinHeight = canvas.Height;
        // Keep RDL border size in sync when canvas grows.
        if (canvas.Children.OfType<WpfRectangle>().FirstOrDefault(r => r.IsHitTestVisible == false) is { } outline)
            outline.Height = canvas.Height;

        return canvas;
    }

    private FrameworkElement BuildTablix(XElement item, XNamespace ns, double width, double height)
    {
        _tablixCount++;
        XElement? body = item.Element(ns + "TablixBody");
        if (body is null)
            return BuildUnsupported($"Tablix · {Attr(item, "Name") ?? "Tablix"}", width, height);

        var cols = body.Element(ns + "TablixColumns")?.Elements(ns + "TablixColumn").ToList() ?? [];
        var rows = body.Element(ns + "TablixRows")?.Elements(ns + "TablixRow").ToList() ?? [];
        if (rows.Count == 0)
            return BuildUnsupported($"Tablix · {Attr(item, "Name") ?? "empty"}", width, height);

        if (cols.Count == 1 && IsContainerTablix(rows, ns))
            return DashedFrame(BuildContainerTablix(rows, ns, width), width, 0, FrameKind.Content);

        return DashedFrame(BuildTableTablix(cols, rows, ns, width), width, 0, FrameKind.Content);
    }

    private static bool IsContainerTablix(List<XElement> rows, XNamespace ns)
    {
        foreach (XElement row in rows)
        {
            foreach (XElement cell in row.Element(ns + "TablixCells")?.Elements(ns + "TablixCell") ?? [])
            {
                XElement? contents = cell.Element(ns + "CellContents");
                if (contents?.Elements().Any(e => e.Name.LocalName == "Rectangle") == true)
                    return true;
            }
        }
        return false;
    }

    private FrameworkElement BuildContainerTablix(List<XElement> rows, XNamespace ns, double width)
    {
        var stack = new StackPanel { Orientation = WpfOrientation.Vertical };
        if (width > 0)
            stack.Width = width;

        foreach (XElement row in rows)
        {
            double rowH = ToPx(Value(row, ns, "Height"));
            var rowCanvas = new Canvas
            {
                ClipToBounds = false,
                Background = PageBackground
            };
            if (width > 0)
                rowCanvas.Width = width;

            double contentBottom = rowH > 0 ? rowH : 50;
            foreach (XElement cell in row.Element(ns + "TablixCells")?.Elements(ns + "TablixCell") ?? [])
            {
                XElement? contents = cell.Element(ns + "CellContents");
                if (contents is null)
                    continue;

                foreach (XElement child in contents.Elements())
                {
                    if (child.Name.LocalName == "Rectangle")
                    {
                        XElement? nested = child.Element(ns + "ReportItems");
                        if (nested is null)
                            continue;
                        foreach (XElement nestedItem in nested.Elements())
                        {
                            UIElement? el = BuildItem(nestedItem, ns, positioned: true, out double bottom);
                            if (el is null)
                                continue;
                            rowCanvas.Children.Add(el);
                            contentBottom = Math.Max(contentBottom, bottom);
                        }
                    }
                    else
                    {
                        UIElement? el = BuildItem(child, ns, positioned: true, out double bottom);
                        if (el is null)
                            continue;
                        rowCanvas.Children.Add(el);
                        contentBottom = Math.Max(contentBottom, bottom);
                    }
                }
            }

            rowCanvas.Height = Math.Max(rowH > 0 ? rowH : 50, contentBottom + 4);
            stack.Children.Add(rowCanvas);
        }

        return stack;
    }

    private FrameworkElement BuildTableTablix(
        List<XElement> cols,
        List<XElement> rows,
        XNamespace ns,
        double width)
    {
        var grid = new WpfGrid
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        if (width > 0)
            grid.MinWidth = width;

        for (int c = 0; c < cols.Count; c++)
        {
            double colW = ToPx(Value(cols[c], ns, "Width"));
            // Keep RDL width as minimum; allow column to grow so cell text is readable
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = colW > 0 ? colW : 48
            });
        }

        if (cols.Count == 0)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int r = 0; r < rows.Count; r++)
        {
            double rowH = ToPx(Value(rows[r], ns, "Height"));
            // Auto height so wrapped labels aren't clipped; RDL height is a floor via MinHeight
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var cells = rows[r].Element(ns + "TablixCells")?.Elements(ns + "TablixCell").ToList() ?? [];
            for (int c = 0; c < cells.Count; c++)
            {
                var cellBorder = new Border
                {
                    BorderBrush = GridLine,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4, 3, 4, 3),
                    Background = PageBackground,
                    CornerRadius = new CornerRadius(0),
                    SnapsToDevicePixels = true,
                    ClipToBounds = false,
                    MinHeight = rowH > 0 ? rowH : 0,
                    Child = BuildCellContent(cells[c], ns)
                };

                int colSpan = ParseSpan(Value(cells[c], ns, "ColSpan"));
                int rowSpan = ParseSpan(Value(cells[c], ns, "RowSpan"));

                WpfGrid.SetRow(cellBorder, r);
                WpfGrid.SetColumn(cellBorder, c);
                if (colSpan > 1)
                    WpfGrid.SetColumnSpan(cellBorder, colSpan);
                if (rowSpan > 1)
                    WpfGrid.SetRowSpan(cellBorder, rowSpan);

                if (c < cols.Count)
                {
                    double colW = ToPx(Value(cols[c], ns, "Width"));
                    if (colW > 0)
                        cellBorder.MinWidth = colW;
                }

                grid.Children.Add(cellBorder);
            }
        }

        return grid;
    }

    private UIElement BuildCellContent(XElement cell, XNamespace ns)
    {
        XElement? contents = cell.Element(ns + "CellContents");
        if (contents is null)
            return new TextBlock();

        var panel = new StackPanel();
        foreach (XElement child in contents.Elements())
        {
            string local = child.Name.LocalName;
            if (local == "Textbox")
            {
                var block = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.None,
                    Foreground = Ink
                };
                FillTextboxInlines(block.Inlines, child, ns);
                TagDesignItem(block, child);
                panel.Children.Add(block);
            }
            else if (local == "Rectangle")
            {
                XElement? items = child.Element(ns + "ReportItems");
                if (items is not null)
                {
                    foreach (XElement nested in items.Elements())
                    {
                        if (nested.Name.LocalName == "Textbox")
                        {
                            var block = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                TextTrimming = TextTrimming.None,
                                Foreground = Ink
                            };
                            FillTextboxInlines(block.Inlines, nested, ns);
                            TagDesignItem(block, nested);
                            panel.Children.Add(block);
                        }
                        else if (nested.Name.LocalName == "Tablix")
                        {
                            panel.Children.Add(BuildTablix(nested, ns, 0, 0));
                        }
                        else
                        {
                            UIElement? el = BuildItem(nested, ns, positioned: false, out _);
                            if (el is not null)
                                panel.Children.Add(el);
                        }
                    }
                }
            }
            else if (local == "Image")
            {
                panel.Children.Add(BuildPlaceholder("Image", 40, 24));
            }
            else if (local == "Tablix")
            {
                panel.Children.Add(BuildTablix(child, ns, 0, 0));
            }
        }

        // Always return the panel — returning Children[0] while it is still parented
        // throws "Specified element is already the logical child of another element".
        return panel;
    }

    private FrameworkElement? BuildGenericContainer(XElement item, XNamespace ns, double width, double height)
    {
        XElement? items = item.Element(ns + "ReportItems");
        if (items is null)
            return null;
        return BuildRectangle(item, ns, width, height);
    }

    private static FrameworkElement BuildImage(XElement item, XNamespace ns, double width, double height)
    {
        string name = Attr(item, "Name") ?? "Image";
        string source = Value(item, ns, "Source") ?? "External";
        string? value = Value(item, ns, "Value");

        if (string.Equals(source, "External", StringComparison.OrdinalIgnoreCase) &&
            TryLoadExternalBitmap(value, out ImageSource? bitmap) &&
            bitmap is not null)
        {
            return new System.Windows.Controls.Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                ToolTip = $"{name}\n{value}"
            };
        }

        string label = string.IsNullOrWhiteSpace(value)
            ? $"Image · {name}"
            : $"Image · {System.IO.Path.GetFileName(TryLocalPath(value) ?? value)}";
        return BuildPlaceholder(label, width, height);
    }

    private static bool TryLoadExternalBitmap(string? value, out ImageSource? bitmap)
    {
        bitmap = null;
        string? path = TryLocalPath(value);
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return false;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            bitmap = image;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryLocalPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        value = value.Trim().Trim('"');
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(value);
                return uri.IsFile ? uri.LocalPath : null;
            }
            catch
            {
                return null;
            }
        }

        return System.IO.Path.IsPathRooted(value) ? value : null;
    }

    private static FrameworkElement BuildPlaceholder(string label, double width, double height)
    {
        return new Border
        {
            Background = PageBackground,
            CornerRadius = new CornerRadius(0),
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = MutedInk,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static FrameworkElement BuildUnsupported(string label, double width, double height)
    {
        return new Border
        {
            Background = PageBackground,
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(4, 2, 4, 2),
            MinWidth = width > 0 ? width : 80,
            MinHeight = height > 0 ? Math.Max(height, MinReadableHeight) : MinReadableHeight,
            SnapsToDevicePixels = true,
            Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = Ink,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static FrameworkElement BuildLine(double width, double height)
    {
        return new WpfRectangle
        {
            Height = 1,
            Width = width > 0 ? width : 100,
            Fill = Ink,
            Margin = new Thickness(0, Math.Max(0, height / 2), 0, 0),
            SnapsToDevicePixels = true
        };
    }

    private enum FrameKind
    {
        /// <summary>Text/image: inset content, may grow taller than RDL for readability.</summary>
        Content,
        /// <summary>Rectangle: exact RDL size, outline only (children keep absolute coords).</summary>
        Container
    }

    private static FrameworkElement DashedFrame(
        FrameworkElement content,
        double width,
        double height,
        FrameKind kind)
    {
        var root = new Grid
        {
            SnapsToDevicePixels = true,
            ClipToBounds = true,
            Background = PageBackground
        };

        if (kind == FrameKind.Container)
        {
            if (width > 0)
                root.Width = width;
            // Prefer content size over a fixed RDL height so nested items aren't clipped.
            root.ClipToBounds = false;
            if (height > 0)
                root.MinHeight = height;

            // Content first, dashed chrome on top — otherwise the opaque canvas hides the outline.
            root.Children.Add(content);
            root.Children.Add(CreateDashedOutline(fill: false));
            return root;
        }

        // Content frames keep RDL geometry (avoids overlap in dense layouts).
        // Text is inset from the stroke; tiny boxes use a tighter inset + ellipsis.
        if (width > 0)
            root.Width = width;
        else
            root.MinWidth = 40;

        if (height > 0)
            root.Height = height;
        else
            root.MinHeight = MinReadableHeight;

        content.ClearValue(FrameworkElement.WidthProperty);
        content.ClearValue(FrameworkElement.HeightProperty);
        content.ClearValue(FrameworkElement.MinWidthProperty);
        content.ClearValue(FrameworkElement.MinHeightProperty);
        content.ClearValue(FrameworkElement.MaxWidthProperty);
        content.ClearValue(FrameworkElement.MaxHeightProperty);
        content.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Stretch;

        root.Children.Add(CreateDashedOutline(fill: true));

        double insetX = 3;
        double insetY = height > 0 && height < 20 ? 1 : 3;

        var host = new Border
        {
            Margin = new Thickness(insetX, insetY, insetX, insetY),
            Padding = new Thickness(2, 0, 2, 0),
            Child = content,
            Background = PageBackground,
            CornerRadius = new CornerRadius(0),
            ClipToBounds = true
        };
        root.Children.Add(host);
        return root;
    }

    private static WpfRectangle CreateDashedOutline(bool fill) =>
        new()
        {
            Stroke = OutlineStroke,
            StrokeThickness = 1,
            StrokeDashArray = DashPattern,
            Fill = fill ? PageBackground : WpfBrushes.Transparent,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };

    private static (XElement? Body, XElement? Page) ResolveBodyAndPage(XElement report, XNamespace ns)
    {
        XElement? sections = report.Element(ns + "ReportSections");
        XElement? section = sections?.Element(ns + "ReportSection");
        if (section is not null)
            return (section.Element(ns + "Body"), section.Element(ns + "Page"));

        return (report.Element(ns + "Body"), report.Element(ns + "Page"));
    }

    private static bool IsHidden(XElement item, XNamespace ns)
    {
        string? hidden = item.Element(ns + "Visibility")?.Element(ns + "Hidden")?.Value;
        return hidden is "true" or "True";
    }

    private static string? Value(XElement? parent, XNamespace ns, string name) =>
        parent?.Element(ns + name)?.Value;

    private static string? Attr(XElement el, string name) => el.Attribute(name)?.Value;

    private static int ParseSpan(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 1 ? n : 1;

    private static double ToPx(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('='))
            return 0;

        string v = raw.Trim().ToLowerInvariant();
        if (!double.TryParse(
                new string(v.TakeWhile(c => char.IsDigit(c) || c is '.' or '-' or '+').ToArray()),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double n))
            return 0;

        if (v.EndsWith("in", StringComparison.Ordinal)) return n * 96;
        if (v.EndsWith("cm", StringComparison.Ordinal)) return n * 37.7952755906;
        if (v.EndsWith("mm", StringComparison.Ordinal)) return n * 3.77952755906;
        if (v.EndsWith("pt", StringComparison.Ordinal)) return n * 96.0 / 72.0;
        if (v.EndsWith("pc", StringComparison.Ordinal)) return n * 16;
        return n;
    }

    private static bool TryParseColor(string? value, out WpfColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('='))
            return false;

        try
        {
            var converted = WpfColorConverter.ConvertFromString(value);
            if (converted is WpfColor c)
            {
                color = c;
                return true;
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(WpfColor.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Abstractions;

namespace ReportExpert.RdlcDesigner.Model;

public sealed class ReportItemModel : IReportItem
{
    public ReportItemModel(
        string id,
        ReportItemKind kind,
        XElement element,
        XNamespace ns,
        bool canEditGeometry)
    {
        Id = id;
        Kind = kind;
        Element = element;
        Namespace = ns;
        CanEditGeometry = canEditGeometry;
        CanEditName = kind is ReportItemKind.Textbox or ReportItemKind.Rectangle or ReportItemKind.Tablix
            or ReportItemKind.Line or ReportItemKind.Image or ReportItemKind.Chart
            or ReportItemKind.Subreport or ReportItemKind.Gauge;
        CanEditValue = kind is ReportItemKind.Textbox or ReportItemKind.TablixCellTextbox;
        CanEditStyle = kind is ReportItemKind.Textbox or ReportItemKind.TablixCellTextbox;
        CanEditImage = kind is ReportItemKind.Image;
        CanEditAppearance = kind is ReportItemKind.Rectangle;
        RefreshFromXml();
    }

    public string Id { get; }
    public ReportItemKind Kind { get; }
    public XElement Element { get; }
    public XNamespace Namespace { get; }
    public bool CanEditGeometry { get; }
    public bool CanEditValue { get; }
    public bool CanEditName { get; }
    public bool CanEditStyle { get; }
    public bool CanEditImage { get; }
    public bool CanEditAppearance { get; }

    public string Name { get; private set; } = string.Empty;
    public double LeftPx { get; private set; }
    public double TopPx { get; private set; }
    public double WidthPx { get; private set; }
    public double HeightPx { get; private set; }
    public string? Value { get; private set; }
    public string? FontFamily { get; private set; }
    public string? FontSize { get; private set; }
    public string? FontWeight { get; private set; }
    public string? Color { get; private set; }
    public bool Hidden { get; private set; }
    public string? ImageSource { get; private set; }
    public string? ImageValue { get; private set; }
    public string? ImageMimeType { get; private set; }
    public string? BackgroundColor { get; private set; }
    public string? BorderColor { get; private set; }
    public string? BorderStyle { get; private set; }
    public string? BorderWidth { get; private set; }

    public string? LeftRaw { get; private set; }
    public string? TopRaw { get; private set; }
    public string? WidthRaw { get; private set; }
    public string? HeightRaw { get; private set; }

    public void RefreshFromXml()
    {
        Name = Element.Attribute("Name")?.Value ?? Id;
        LeftRaw = Element.Element(Namespace + "Left")?.Value;
        TopRaw = Element.Element(Namespace + "Top")?.Value;
        WidthRaw = Element.Element(Namespace + "Width")?.Value;
        HeightRaw = Element.Element(Namespace + "Height")?.Value;
        LeftPx = RdlUnits.ToPx(LeftRaw);
        TopPx = RdlUnits.ToPx(TopRaw);
        WidthPx = RdlUnits.ToPx(WidthRaw);
        HeightPx = RdlUnits.ToPx(HeightRaw);
        Value = ReadValue();

        XElement? runStyle = FirstTextRunStyle();
        XElement? itemStyle = Element.Element(Namespace + "Style");
        FontFamily = ValueOf(runStyle, "FontFamily") ?? ValueOf(itemStyle, "FontFamily");
        FontSize = ValueOf(runStyle, "FontSize") ?? ValueOf(itemStyle, "FontSize");
        FontWeight = ValueOf(runStyle, "FontWeight") ?? ValueOf(itemStyle, "FontWeight");
        Color = ValueOf(runStyle, "Color") ?? ValueOf(itemStyle, "Color");

        string? hidden = Element.Element(Namespace + "Visibility")?.Element(Namespace + "Hidden")?.Value;
        Hidden = hidden is "true" or "True";

        if (Kind == ReportItemKind.Image)
        {
            ImageSource = Element.Element(Namespace + "Source")?.Value ?? "External";
            ImageValue = Element.Element(Namespace + "Value")?.Value;
            ImageMimeType = Element.Element(Namespace + "MIMEType")?.Value;
        }
        else
        {
            ImageSource = null;
            ImageValue = null;
            ImageMimeType = null;
        }

        XElement? border = itemStyle?.Element(Namespace + "Border");
        BackgroundColor = ValueOf(itemStyle, "BackgroundColor");
        BorderColor = border is null ? null : ValueOf(border, "Color");
        BorderStyle = border is null ? null : ValueOf(border, "Style");
        BorderWidth = border is null ? null : ValueOf(border, "Width");
    }

    public void ApplyGeometry(double leftPx, double topPx, double widthPx, double heightPx)
    {
        LeftPx = Math.Max(0, leftPx);
        TopPx = Math.Max(0, topPx);
        WidthPx = Math.Max(1, widthPx);
        HeightPx = Math.Max(Kind == ReportItemKind.Line ? 0 : 1, heightPx);

        SetChild(Namespace + "Left", RdlUnits.FromPx(LeftPx, LeftRaw));
        SetChild(Namespace + "Top", RdlUnits.FromPx(TopPx, TopRaw));
        SetChild(Namespace + "Width", RdlUnits.FromPx(WidthPx, WidthRaw));
        SetChild(Namespace + "Height", RdlUnits.FromPx(Math.Max(HeightPx, Kind == ReportItemKind.Line ? 0 : 1), HeightRaw));

        LeftRaw = Element.Element(Namespace + "Left")?.Value;
        TopRaw = Element.Element(Namespace + "Top")?.Value;
        WidthRaw = Element.Element(Namespace + "Width")?.Value;
        HeightRaw = Element.Element(Namespace + "Height")?.Value;
    }

    public void ApplyName(string name)
    {
        Name = name;
        Element.SetAttributeValue("Name", name);
    }

    public void ApplyValue(string? value)
    {
        Value = value;
        WriteValue(value);
    }

    public void ApplyFont(string? family, string? size, string? weight, string? color)
    {
        // Guard against UI control ToString() leaking into RDL (e.g. ComboBoxItem).
        if (!string.IsNullOrWhiteSpace(color) &&
            (color.Contains("System.Windows.", StringComparison.Ordinal) ||
             color.Contains("ComboBoxItem", StringComparison.OrdinalIgnoreCase)))
            color = null;

        FontFamily = family;
        FontSize = size;
        FontWeight = weight;
        Color = color;

        EnsureParagraphStructure();
        XElement? runStyle = FirstTextRunStyle();
        if (runStyle is null)
            return;

        SetOrRemove(runStyle, "FontFamily", family);
        SetOrRemove(runStyle, "FontSize", size);
        SetOrRemove(runStyle, "FontWeight", weight);
        SetOrRemove(runStyle, "Color", color);
    }

    public void ApplyHidden(bool hidden)
    {
        Hidden = hidden;
        XElement? visibility = Element.Element(Namespace + "Visibility");
        if (!hidden)
        {
            visibility?.Remove();
            return;
        }

        if (visibility is null)
        {
            visibility = new XElement(Namespace + "Visibility");
            Element.Add(visibility);
        }

        SetChildOn(visibility, Namespace + "Hidden", "true");
    }

    public void ApplyImage(string source, string? value, string? mimeType)
    {
        // Designer currently supports External images only.
        source = "External";
        ImageSource = source;
        ImageValue = value;
        ImageMimeType = null;
        SetChild(Namespace + "Source", source);
        SetChild(Namespace + "Value", value ?? string.Empty);
        // MIMEType is for Embedded/Database; remove if present.
        Element.Element(Namespace + "MIMEType")?.Remove();
        _ = mimeType;
    }

    public void ApplyAppearance(string? backgroundColor, string? borderColor, string? borderStyle, string? borderWidth)
    {
        BackgroundColor = NullIfEmpty(backgroundColor);
        BorderColor = NullIfEmpty(borderColor);
        BorderStyle = NullIfEmpty(borderStyle);
        BorderWidth = NullIfEmpty(borderWidth);

        XElement style = Element.Element(Namespace + "Style") ?? new XElement(Namespace + "Style");
        if (style.Parent is null)
            Element.Add(style);

        SetOrRemove(style, "BackgroundColor", BackgroundColor);

        XElement? border = style.Element(Namespace + "Border");
        bool hasBorder =
            !string.IsNullOrWhiteSpace(BorderColor) ||
            !string.IsNullOrWhiteSpace(BorderStyle) ||
            !string.IsNullOrWhiteSpace(BorderWidth);

        if (!hasBorder)
        {
            border?.Remove();
            return;
        }

        if (border is null)
        {
            border = new XElement(Namespace + "Border");
            style.Add(border);
        }

        SetOrRemoveOn(border, "Color", BorderColor);
        SetOrRemoveOn(border, "Style", BorderStyle ?? "Solid");
        SetOrRemoveOn(border, "Width", BorderWidth);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void SetOrRemoveOn(XElement parent, string localName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parent.Element(Namespace + localName)?.Remove();
            return;
        }

        SetChildOn(parent, Namespace + localName, value);
    }

    private XElement? FirstTextRunStyle()
    {
        return Element.Element(Namespace + "Paragraphs")
            ?.Elements(Namespace + "Paragraph")
            .SelectMany(p => p.Element(Namespace + "TextRuns")?.Elements(Namespace + "TextRun") ?? [])
            .Select(r => r.Element(Namespace + "Style"))
            .FirstOrDefault(s => s is not null);
    }

    private void EnsureParagraphStructure()
    {
        if (Element.Element(Namespace + "Paragraphs") is not null)
            return;

        var paragraphs = new XElement(Namespace + "Paragraphs");
        var paragraph = new XElement(Namespace + "Paragraph");
        var textRuns = new XElement(Namespace + "TextRuns");
        var textRun = new XElement(Namespace + "TextRun");
        textRun.Add(new XElement(Namespace + "Value", Value ?? string.Empty));
        textRun.Add(new XElement(Namespace + "Style"));
        textRuns.Add(textRun);
        paragraph.Add(textRuns);
        paragraph.Add(new XElement(Namespace + "Style"));
        paragraphs.Add(paragraph);
        Element.Add(paragraphs);
    }

    private string? ReadValue()
    {
        if (Kind == ReportItemKind.Image)
            return Element.Element(Namespace + "Value")?.Value;

        XElement? paragraphs = Element.Element(Namespace + "Paragraphs");
        if (paragraphs is null)
            return Element.Element(Namespace + "Value")?.Value;

        var parts = new List<string>();
        foreach (XElement para in paragraphs.Elements(Namespace + "Paragraph"))
        {
            XElement? runs = para.Element(Namespace + "TextRuns");
            if (runs is null)
                continue;
            foreach (XElement run in runs.Elements(Namespace + "TextRun"))
            {
                string? v = run.Element(Namespace + "Value")?.Value;
                if (!string.IsNullOrEmpty(v))
                    parts.Add(v);
            }
        }

        return parts.Count == 0 ? string.Empty : string.Join("", parts);
    }

    private void WriteValue(string? value)
    {
        value ??= string.Empty;
        if (Kind == ReportItemKind.Image)
        {
            SetChild(Namespace + "Value", value);
            return;
        }

        XElement? paragraphs = Element.Element(Namespace + "Paragraphs");
        if (paragraphs is null)
        {
            SetChild(Namespace + "Value", value);
            return;
        }

        XElement? firstRun = paragraphs
            .Elements(Namespace + "Paragraph")
            .SelectMany(p => p.Element(Namespace + "TextRuns")?.Elements(Namespace + "TextRun") ?? [])
            .FirstOrDefault();

        if (firstRun is not null)
        {
            SetChildOn(firstRun, Namespace + "Value", value);
            bool first = true;
            foreach (XElement para in paragraphs.Elements(Namespace + "Paragraph"))
            {
                XElement? runs = para.Element(Namespace + "TextRuns");
                if (runs is null)
                    continue;
                foreach (XElement run in runs.Elements(Namespace + "TextRun").ToList())
                {
                    if (first)
                    {
                        first = false;
                        continue;
                    }

                    run.Remove();
                }
            }

            return;
        }

        paragraphs.RemoveAll();
        var paragraph = new XElement(Namespace + "Paragraph");
        var textRuns = new XElement(Namespace + "TextRuns");
        var textRun = new XElement(Namespace + "TextRun");
        textRun.Add(new XElement(Namespace + "Value", value));
        textRun.Add(new XElement(Namespace + "Style"));
        textRuns.Add(textRun);
        paragraph.Add(textRuns);
        paragraph.Add(new XElement(Namespace + "Style"));
        paragraphs.Add(paragraph);
    }

    private string? ValueOf(XElement? style, string name) =>
        style?.Element(Namespace + name)?.Value;

    private void SetOrRemove(XElement style, string localName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            style.Element(Namespace + localName)?.Remove();
            return;
        }

        SetChildOn(style, Namespace + localName, value);
    }

    private void SetChild(XName name, string value) => SetChildOn(Element, name, value);

    private static void SetChildOn(XElement parent, XName name, string value)
    {
        XElement? existing = parent.Element(name);
        if (existing is not null)
            existing.Value = value;
        else
            parent.Add(new XElement(name, value));
    }
}

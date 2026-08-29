using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Services;

public sealed class SchemaService : ISchemaService
{
    private static readonly XNamespace Rd = "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";
    private readonly Func<RdlcDocument?> _document;
    private readonly Action? _afterChange;
    private List<RdlcDataSetNode> _dataSets = [];
    private List<RdlcParameterNode> _parameters = [];

    public SchemaService(Func<RdlcDocument?> document, Action? afterChange = null)
    {
        _document = document;
        _afterChange = afterChange;
    }

    public IReadOnlyList<RdlcDataSetNode> DataSets => _dataSets;
    public IReadOnlyList<RdlcParameterNode> Parameters => _parameters;

    public event EventHandler? SchemaChanged;

    public void Refresh()
    {
        RdlcDocument? doc = _document();
        if (doc is null)
        {
            _dataSets = [];
            _parameters = [];
            SchemaChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        XNamespace ns = doc.Namespace;
        _dataSets = doc.Xml.Descendants(ns + "DataSet")
            .Select(ds =>
            {
                string name = ds.Attribute("Name")?.Value ?? "DataSet";
                var fields = ds.Descendants(ns + "Field")
                    .Select(f => new RdlcFieldNode(
                        f.Attribute("Name")?.Value ?? string.Empty,
                        f.Element(ns + "DataField")?.Value ?? f.Attribute("Name")?.Value ?? string.Empty,
                        f.Element(Rd + "TypeName")?.Value))
                    .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                    .ToList();
                return new RdlcDataSetNode(name, fields);
            })
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .ToList();

        _parameters = doc.Xml.Descendants(ns + "ReportParameter")
            .Select(p => new RdlcParameterNode(
                p.Attribute("Name")?.Value ?? string.Empty,
                p.Element(ns + "DataType")?.Value ?? "String",
                bool.TryParse(p.Element(ns + "Nullable")?.Value, out bool n) && n,
                !bool.TryParse(p.Element(ns + "AllowBlank")?.Value, out bool ab) || ab,
                p.Element(ns + "Prompt")?.Value,
                p.Element(ns + "DefaultValue")?.Element(ns + "Values")?.Element(ns + "Value")?.Value
                    ?? p.Descendants(ns + "Value").FirstOrDefault()?.Value))
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        SchemaChanged?.Invoke(this, EventArgs.Empty);
    }

    public RdlcParameterNode? AddParameter(string name)
    {
        RdlcDocument? doc = _document();
        if (doc is null || string.IsNullOrWhiteSpace(name))
            return null;

        name = name.Trim();
        if (_parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return null;

        XNamespace ns = doc.Namespace;
        XElement container = doc.GetOrCreateParameters();
        var param = new XElement(ns + "ReportParameter",
            new XAttribute("Name", name),
            new XElement(ns + "DataType", "String"),
            new XElement(ns + "Nullable", "true"),
            new XElement(ns + "AllowBlank", "true"),
            new XElement(ns + "Prompt", name));
        container.Add(param);

        var node = new RdlcParameterNode(name, "String", true, true, name, null);
        Refresh();
        _afterChange?.Invoke();
        return node;
    }

    public bool RemoveParameter(string name)
    {
        RdlcDocument? doc = _document();
        if (doc is null)
            return false;

        XElement? el = doc.Xml.Descendants(doc.Namespace + "ReportParameter")
            .FirstOrDefault(p => string.Equals(p.Attribute("Name")?.Value, name, StringComparison.OrdinalIgnoreCase));
        if (el is null)
            return false;

        el.Remove();
        Refresh();
        _afterChange?.Invoke();
        return true;
    }

    public void UpdateParameter(
        string name,
        string dataType,
        bool nullable,
        bool allowBlank,
        string? prompt,
        string? defaultValue)
    {
        RdlcDocument? doc = _document();
        if (doc is null)
            return;

        XNamespace ns = doc.Namespace;
        XElement? el = doc.Xml.Descendants(ns + "ReportParameter")
            .FirstOrDefault(p => string.Equals(p.Attribute("Name")?.Value, name, StringComparison.OrdinalIgnoreCase));
        if (el is null)
            return;

        Set(el, ns + "DataType", dataType);
        Set(el, ns + "Nullable", nullable ? "true" : "false");
        Set(el, ns + "AllowBlank", allowBlank ? "true" : "false");
        Set(el, ns + "Prompt", prompt ?? name);

        XElement? defaults = el.Element(ns + "DefaultValue");
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            defaults?.Remove();
        }
        else
        {
            if (defaults is null)
            {
                defaults = new XElement(ns + "DefaultValue");
                el.Add(defaults);
            }

            XElement? values = defaults.Element(ns + "Values");
            if (values is null)
            {
                values = new XElement(ns + "Values");
                defaults.Add(values);
            }

            values.RemoveAll();
            values.Add(new XElement(ns + "Value", defaultValue));
        }

        Refresh();
        _afterChange?.Invoke();
    }

    private static void Set(XElement parent, XName name, string value)
    {
        XElement? existing = parent.Element(name);
        if (existing is not null)
            existing.Value = value;
        else
            parent.Add(new XElement(name, value));
    }
}

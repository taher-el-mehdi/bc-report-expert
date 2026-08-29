using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ReportExpert.RdlcDesigner.Abstractions;
using ReportExpert.RdlcDesigner.Model;

namespace ReportExpert.RdlcDesigner.Persistence;

/// <summary>
/// Loads and saves RDLC XML while preserving unrelated nodes and namespaces.
/// </summary>
public sealed class RdlcPersistence : IRdlcPersistence
{
    public RdlcDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("RDLC file not found.", path);

        // Do not use PreserveWhitespace: edits rewrite size/value nodes cleanly.
        var xml = XDocument.Load(path, LoadOptions.None);
        return new RdlcDocument(xml, path);
    }

    public void Save(RdlcDocument document, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        string target = path ?? document.FilePath
            ?? throw new InvalidOperationException("No path specified for save.");

        // Geometry/value mutations already applied on live XElements; write document as-is.
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using (var writer = XmlWriter.Create(target, settings))
            document.Xml.Save(writer);

        document.FilePath = target;
    }
}

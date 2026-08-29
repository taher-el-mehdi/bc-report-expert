using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ReportExpert.RdlcDesigner.Persistence;

/// <summary>Pretty-prints RDLC <see cref="XDocument"/> instances for the XML view.</summary>
public static class RdlcXmlFormatter
{
    public static string Format(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using var stringWriter = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
            document.Save(xmlWriter);

        return stringWriter.ToString();
    }
}

using System.Xml.Linq;

namespace ReportExpert.Rdl.Core;

/// <summary>
/// A loaded report definition together with the namespaces it declares.
/// </summary>
/// <remarks>
/// <para>
/// RDL exists in several schema revisions that differ only by namespace URI. Rather than assume a
/// version, the namespace is taken from the root element, so a document is always written back in
/// the same schema it was read in.
/// </para>
/// <para>
/// Instances are not thread safe. Each operation loads its own copy; concurrency is coordinated by
/// the caller at the file-path level.
/// </para>
/// </remarks>
public sealed class RdlDocument
{
    private RdlDocument(XDocument xml, XElement root, string? filePath)
    {
        Xml = xml;
        Root = root;
        FilePath = filePath;
        Ns = root.Name.Namespace;
    }

    /// <summary>The underlying XML document.</summary>
    public XDocument Xml { get; }

    /// <summary>The <c>Report</c> root element.</summary>
    public XElement Root { get; }

    /// <summary>
    /// The report definition namespace declared by this document.
    /// </summary>
    /// <remarks>
    /// Compose element names against this rather than a constant, for example
    /// <c>doc.Ns + "DataSet"</c>.
    /// </remarks>
    public XNamespace Ns { get; }

    /// <summary>The Report Designer namespace, used for <c>rd:</c> annotations.</summary>
    public static XNamespace Rd => RdlNamespaces.ReportDesigner;

    /// <summary>
    /// Where this document was loaded from, or <see langword="null"/> when it was parsed from text.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Loads a report definition from disk.
    /// </summary>
    /// <param name="filePath">Absolute path to the <c>.rdl</c> or <c>.rdlc</c> file.</param>
    /// <returns>The loaded document.</returns>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="filePath"/>.</exception>
    /// <exception cref="InvalidRdlException">The file is not a usable report definition.</exception>
    public static RdlDocument Load(string filePath)
    {
        var xml = RdlXmlIO.Load(filePath);
        return Create(xml, filePath);
    }

    /// <summary>
    /// Parses a report definition from text.
    /// </summary>
    /// <param name="xml">The report definition XML.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="InvalidRdlException">The text is not a usable report definition.</exception>
    public static RdlDocument Parse(string xml) => Create(RdlXmlIO.Parse(xml), filePath: null);

    private static RdlDocument Create(XDocument xml, string? filePath)
    {
        XElement root = xml.Root
            ?? throw new InvalidRdlException("The report definition has no root element.");

        if (!string.Equals(root.Name.LocalName, "Report", StringComparison.Ordinal))
        {
            throw new InvalidRdlException(
                $"Expected a <Report> root element but found <{root.Name.LocalName}>. " +
                "This file does not look like an RDL or RDLC report definition.");
        }

        return new RdlDocument(xml, root, filePath);
    }

    /// <summary>
    /// Writes the document back to the path it was loaded from.
    /// </summary>
    /// <exception cref="InvalidOperationException">The document was parsed from text and has no path.</exception>
    public void Save()
    {
        if (FilePath is null)
            throw new InvalidOperationException("This document was parsed from text and has no file path to save to.");

        RdlXmlIO.SaveAtomic(Xml, FilePath);
    }

    /// <summary>
    /// Writes the document to a specific path.
    /// </summary>
    /// <param name="filePath">Destination path.</param>
    public void SaveAs(string filePath) => RdlXmlIO.SaveAtomic(Xml, filePath);

    /// <summary>
    /// Renders the document as it would be written to disk. Used to diff a pending edit.
    /// </summary>
    /// <returns>The serialized report definition.</returns>
    public string Serialize() => RdlXmlIO.Serialize(Xml);

    /// <summary>
    /// Creates an element in the report definition namespace.
    /// </summary>
    /// <param name="localName">The unqualified element name, for example <c>"TablixColumn"</c>.</param>
    /// <param name="content">Optional child content.</param>
    /// <returns>The new element.</returns>
    public XElement Element(string localName, params object?[] content) => new(Ns + localName, content);

    /// <summary>
    /// Finds every descendant of the root with the given local name, in document order.
    /// </summary>
    /// <param name="localName">The unqualified element name.</param>
    /// <returns>The matching elements.</returns>
    public IEnumerable<XElement> Descendants(string localName) => Root.Descendants(Ns + localName);
}

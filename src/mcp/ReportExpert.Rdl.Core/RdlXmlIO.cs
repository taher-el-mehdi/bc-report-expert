using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ReportExpert.Rdl.Core;

/// <summary>
/// Reading and writing of report definition XML.
/// </summary>
/// <remarks>
/// <para>
/// Reads are hardened: DTD processing is disabled, external resolution is disabled, and the
/// document is checked against <see cref="ResourceLimits"/>.
/// </para>
/// <para>
/// Writes are atomic. The document is serialized to a temporary sibling file and then moved over
/// the target, so a crash mid-write can never leave a half-written report definition on disk.
/// </para>
/// </remarks>
public static class RdlXmlIO
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = true,
        IgnoreComments = false,
        IgnoreProcessingInstructions = false,
        CloseInput = true,
    };

    /// <summary>
    /// Loads a report definition from disk.
    /// </summary>
    /// <param name="filePath">Absolute path to the <c>.rdl</c> or <c>.rdlc</c> file.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="FileNotFoundException">No file exists at <paramref name="filePath"/>.</exception>
    /// <exception cref="ResourceLimitExceededException">The file breaches a <see cref="ResourceLimits"/> cap.</exception>
    /// <exception cref="InvalidRdlException">The file is not well-formed XML.</exception>
    public static XDocument Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var info = new FileInfo(filePath);
        if (!info.Exists)
            throw new FileNotFoundException($"No report definition found at '{filePath}'.", filePath);

        if (info.Length > ResourceLimits.MaxFileBytes)
        {
            throw new ResourceLimitExceededException(
                $"'{filePath}' is {info.Length} bytes, which exceeds the {ResourceLimits.MaxFileBytes} byte limit.");
        }

        XDocument document;
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = XmlReader.Create(stream, ReaderSettings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException ex)
        {
            throw new InvalidRdlException($"'{filePath}' is not well-formed XML: {ex.Message}", ex);
        }

        EnsureWithinLimits(document);
        return document;
    }

    /// <summary>
    /// Parses a report definition from a string. Used for previews and tests.
    /// </summary>
    /// <param name="xml">The report definition XML.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="InvalidRdlException">The text is not well-formed XML.</exception>
    public static XDocument Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        try
        {
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, ReaderSettings);
            var document = XDocument.Load(reader, LoadOptions.None);
            EnsureWithinLimits(document);
            return document;
        }
        catch (XmlException ex)
        {
            throw new InvalidRdlException($"The supplied text is not well-formed XML: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes a report definition to the same indented, UTF-8 form the Reporting Services
    /// designer produces.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <returns>The report definition XML, including the declaration.</returns>
    public static string Serialize(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            // Report definitions are UTF-8 without a byte order mark.
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            NewLineChars = "\n",
        };

        using var buffer = new MemoryStream();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            document.Save(writer);
        }

        return settings.Encoding.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Writes a report definition over an existing path atomically.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <param name="filePath">Destination path.</param>
    /// <remarks>
    /// The temporary file is created in the destination directory so that the final move stays on
    /// one volume and is therefore a rename rather than a copy.
    /// </remarks>
    /// <exception cref="IOException">The destination is locked by another process.</exception>
    public static void SaveAtomic(XDocument document, string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new ArgumentException($"'{filePath}' has no containing directory.", nameof(filePath));

        Directory.CreateDirectory(directory);

        string staging = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(staging, Serialize(document), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(staging, filePath, overwrite: true);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover staging file is harmless; surfacing this would mask the real failure.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static void EnsureWithinLimits(XDocument document)
    {
        if (document.Root is null)
            throw new InvalidRdlException("The document has no root element.");

        // Explicit depth-first walk so both limits are checked in a single pass; asking each
        // element for its ancestors instead would make this quadratic in the nesting depth.
        int count = 0;
        var pending = new Stack<(XElement Element, int Depth)>();
        pending.Push((document.Root, 1));

        while (pending.Count > 0)
        {
            (XElement element, int depth) = pending.Pop();

            if (++count > ResourceLimits.MaxElementCount)
            {
                throw new ResourceLimitExceededException(
                    $"The report definition contains more than {ResourceLimits.MaxElementCount} elements.");
            }

            if (depth > ResourceLimits.MaxElementDepth)
            {
                throw new ResourceLimitExceededException(
                    $"The report definition nests elements more than {ResourceLimits.MaxElementDepth} deep.");
            }

            foreach (var child in element.Elements())
                pending.Push((child, depth + 1));
        }
    }
}

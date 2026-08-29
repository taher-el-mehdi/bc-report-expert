using System.Xml.Linq;

namespace ReportExpert.Rdl.Core;

/// <summary>
/// XML namespaces used by RDL/RDLC documents.
/// </summary>
/// <remarks>
/// The primary report definition namespace is versioned (2008, 2010, 2016, ...) and is therefore
/// never assumed: it is read from the root element of each document. Only the Report Designer
/// namespace, which has been stable across every schema revision, is treated as a constant.
/// </remarks>
public static class RdlNamespaces
{
    /// <summary>
    /// The Report Designer namespace, conventionally bound to the <c>rd</c> prefix. Carries
    /// designer-only annotations such as <c>rd:TypeName</c> on dataset fields.
    /// </summary>
    public static readonly XNamespace ReportDesigner =
        "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner";

    /// <summary>
    /// The RDL 2016 report definition namespace. Used only when creating a document from scratch;
    /// documents that are loaded keep whatever namespace they declare.
    /// </summary>
    public static readonly XNamespace Rdl2016 =
        "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition";
}

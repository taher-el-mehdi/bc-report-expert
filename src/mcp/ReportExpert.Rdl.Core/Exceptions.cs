namespace ReportExpert.Rdl.Core;

/// <summary>
/// The kind of thing that could not be located. Callers use this to produce an actionable hint
/// naming the tool that would list valid values.
/// </summary>
public enum RdlTarget
{
    /// <summary>The report definition file itself.</summary>
    Report,

    /// <summary>A <c>DataSet</c> element.</summary>
    DataSet,

    /// <summary>A <c>Field</c> element inside a dataset.</summary>
    Field,

    /// <summary>A <c>ReportParameter</c> element.</summary>
    Parameter,

    /// <summary>A <c>Tablix</c> element.</summary>
    Tablix,

    /// <summary>A column within a tablix.</summary>
    Column,

    /// <summary>A row within a tablix.</summary>
    Row,

    /// <summary>A <c>Textbox</c> element.</summary>
    Textbox,

    /// <summary>Any named layout item (textbox, image, tablix, chart, …).</summary>
    ReportItem,

    /// <summary>A <c>Page</c> element.</summary>
    Page,

    /// <summary>A previously created backup file.</summary>
    Backup,
}

/// <summary>
/// Raised when a named or indexed element does not exist in the report definition.
/// </summary>
/// <remarks>
/// Carrying <see cref="Target"/> lets the MCP tool layer map the failure to a specific error code
/// and hint without parsing the message.
/// </remarks>
public sealed class RdlNotFoundException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="target">What kind of element was being looked for.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    public RdlNotFoundException(RdlTarget target, string message)
        : base(message) => Target = target;

    /// <summary>What kind of element was being looked for.</summary>
    public RdlTarget Target { get; }
}

/// <summary>
/// Raised when a file exists and parses as XML but is not a usable report definition,
/// or when an edit would leave it structurally inconsistent.
/// </summary>
public sealed class InvalidRdlException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="message">A human-readable description of the problem.</param>
    public InvalidRdlException(string message) : base(message) { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="message">A human-readable description of the problem.</param>
    /// <param name="innerException">The underlying failure.</param>
    public InvalidRdlException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Raised when a report definition exceeds one of the caps in <see cref="ResourceLimits"/>.
/// </summary>
public sealed class ResourceLimitExceededException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="message">A human-readable description of the limit that was hit.</param>
    public ResourceLimitExceededException(string message) : base(message) { }
}

/// <summary>
/// Raised when an operation is refused because the request is ambiguous or the arguments
/// are individually valid but jointly contradictory.
/// </summary>
/// <remarks>
/// Distinct from <see cref="ArgumentException"/>, which signals a single bad argument.
/// </remarks>
public sealed class RdlRefusedException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="message">Why the operation was refused.</param>
    /// <param name="hint">What the caller should do instead. Must not be empty.</param>
    public RdlRefusedException(string message, string hint) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        Hint = hint;
    }

    /// <summary>What the caller should do instead.</summary>
    public string Hint { get; }
}

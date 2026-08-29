namespace ReportExpert.Rdl.Core;

/// <summary>
/// Caps applied when reading report definitions.
/// </summary>
/// <remarks>
/// An MCP server may be pointed at any file path an agent can name, so report definitions are
/// treated as untrusted input. These limits bound memory and stack usage; exceeding one raises
/// <see cref="ResourceLimitExceededException"/> rather than letting the process die.
/// </remarks>
public static class ResourceLimits
{
    /// <summary>Largest report definition that will be read, in bytes.</summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>Deepest element nesting that will be accepted.</summary>
    public const int MaxElementDepth = 256;

    /// <summary>Largest number of elements that will be accepted in a single document.</summary>
    public const int MaxElementCount = 2_000_000;
}

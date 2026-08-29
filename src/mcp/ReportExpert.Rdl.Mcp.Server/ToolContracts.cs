using System.Text.Json.Serialization;

namespace ReportExpert.Rdl.Mcp.Server;

/// <summary>
/// The envelope every tool in this server returns.
/// </summary>
/// <remarks>
/// <para>
/// A single shape means a model never has to learn a per-tool convention for telling success from
/// failure. <see cref="Ok"/> is the only field that needs checking; exactly one of
/// <see cref="Data"/> and <see cref="Error"/> is present.
/// </para>
/// <para>
/// <c>{success, message}</c> <c>data.success</c> and <c>data.message</c> are still there
/// for anything that depended on them.
/// </para>
/// </remarks>
public sealed record ToolResponse
{
    /// <summary>Whether the call succeeded.</summary>
    [JsonPropertyName("ok")]
    public required bool Ok { get; init; }

    /// <summary>The result payload. Present when <see cref="Ok"/> is <see langword="true"/>.</summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }

    /// <summary>What went wrong. Present when <see cref="Ok"/> is <see langword="false"/>.</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolError? Error { get; init; }

    /// <summary>Builds a successful response.</summary>
    /// <param name="data">The result payload.</param>
    /// <returns>The response.</returns>
    public static ToolResponse Success(object? data) => new() { Ok = true, Data = data };

    /// <summary>Builds a failed response.</summary>
    /// <param name="code">A stable machine-readable code from <see cref="ToolErrorCodes"/>.</param>
    /// <param name="message">What went wrong, in plain language.</param>
    /// <param name="hint">What the caller should do next. Must not be empty.</param>
    /// <returns>The response.</returns>
    public static ToolResponse Failure(string code, string message, string hint) => new()
    {
        Ok = false,
        Error = new ToolError { Code = code, Message = message, Hint = hint },
    };
}

/// <summary>
/// A tool failure.
/// </summary>
public sealed record ToolError
{
    /// <summary>A stable machine-readable code from <see cref="ToolErrorCodes"/>.</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>What went wrong, in plain language.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// What the caller should do next.
    /// </summary>
    /// <remarks>
    /// Required rather than optional on purpose. The consumer of these errors is a language model
    /// deciding what to try next, and an error without a next step invites guessing.
    /// </remarks>
    [JsonPropertyName("hint")]
    public required string Hint { get; init; }
}

/// <summary>
/// The error codes this server emits.
/// </summary>
public static class ToolErrorCodes
{
    /// <summary>The report definition does not exist at the given path.</summary>
    public const string FileNotFound = "file_not_found";

    /// <summary>The file is open in another process, or the path is not writable.</summary>
    public const string FileLocked = "file_locked";

    /// <summary>The path is not acceptable, for example empty, relative or the wrong extension.</summary>
    public const string InvalidPath = "invalid_path";

    /// <summary>The file is not a usable report definition.</summary>
    public const string InvalidRdl = "invalid_rdl";

    /// <summary>A named or indexed element does not exist in the report.</summary>
    public const string NotFound = "not_found";

    /// <summary>An argument was missing, malformed or out of range.</summary>
    public const string InvalidArgument = "invalid_argument";

    /// <summary>The operation was understood but refused because it would damage the report.</summary>
    public const string Refused = "refused";

    /// <summary>The edit would have left the report invalid, so it was rolled back.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>The report breached a size or complexity cap.</summary>
    public const string ResourceLimit = "resource_limit";

    /// <summary>An unexpected failure. Always a bug in this server.</summary>
    public const string Internal = "internal_error";
}

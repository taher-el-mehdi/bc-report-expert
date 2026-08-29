namespace ReportExpert.Modules.Copilot.Services;

/// <summary>
/// Something the agent loop did, reported as it happens so the UI can show progress.
/// </summary>
/// <remarks>
/// A turn can involve several tool calls and take a while. Reporting each step lets the user see
/// what is happening and stop it, instead of watching a spinner.
/// </remarks>
public abstract record AgentEvent
{
    /// <summary>The model asked to run a tool.</summary>
    /// <param name="CallId">Correlates with the matching <see cref="ToolFinished"/>.</param>
    /// <param name="ToolName">The tool's display name.</param>
    /// <param name="ArgumentsJson">The arguments, formatted for display.</param>
    public sealed record ToolStarted(string CallId, string ToolName, string ArgumentsJson) : AgentEvent;

    /// <summary>A tool finished.</summary>
    /// <param name="CallId">The call this answers.</param>
    /// <param name="IsError">Whether the tool reported a failure.</param>
    /// <param name="Summary">A short description of the outcome, for the tool card.</param>
    public sealed record ToolFinished(string CallId, bool IsError, string Summary) : AgentEvent;

    /// <summary>The user refused a tool call.</summary>
    /// <param name="CallId">The call that was refused.</param>
    /// <param name="ToolName">The tool's display name.</param>
    public sealed record ToolDenied(string CallId, string ToolName) : AgentEvent;

    /// <summary>The model asked for a tool the user has turned off in Settings.</summary>
    /// <param name="CallId">The call that was blocked.</param>
    /// <param name="ToolName">The tool's display name.</param>
    /// <param name="Message">What to show on the card and tell the user.</param>
    public sealed record ToolDisabled(string CallId, string ToolName, string Message) : AgentEvent;

    /// <summary>A tool changed a file, so anything showing that file is now stale.</summary>
    /// <param name="FilePath">The file that changed.</param>
    public sealed record FileChanged(string FilePath) : AgentEvent;
}

/// <summary>
/// What a completed turn produced.
/// </summary>
/// <param name="Content">The assistant's final reply.</param>
/// <param name="ToolCallCount">How many tools ran.</param>
/// <param name="ChangedFiles">Files a tool wrote to, so the host can reload them.</param>
/// <param name="StoppedAtLimit">
/// Whether the loop hit its iteration cap with the model still wanting to call tools.
/// </param>
public sealed record AgentTurnResult(
    string Content,
    int ToolCallCount,
    IReadOnlyList<string> ChangedFiles,
    bool StoppedAtLimit);

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>
/// A pending request to run a tool that would change something.
/// </summary>
/// <param name="CallId">
/// The model's identifier for the call, so an approver showing this in the conversation can reuse
/// the same card when the call later runs.
/// </param>
/// <param name="QualifiedName">The tool as the gateway knows it, for example <c>rdl__add_column</c>.</param>
/// <param name="DisplayName">The bare tool name, for showing to the user.</param>
/// <param name="ArgumentsJson">The arguments the model chose, formatted for display.</param>
/// <param name="IsDestructive">Whether the server flagged the change as hard to undo.</param>
public sealed record ToolApprovalRequest(
    string CallId,
    string QualifiedName,
    string DisplayName,
    string ArgumentsJson,
    bool IsDestructive);

/// <summary>What the user decided about a tool call.</summary>
public enum ToolApprovalDecision
{
    /// <summary>Run this one call.</summary>
    Approve,

    /// <summary>Run this call and every later one in the same turn without asking again.</summary>
    ApproveForTurn,

    /// <summary>
    /// Run this call and auto-approve every mutating tool for the rest of the session until the
    /// user turns agent mode off or clears the preference.
    /// </summary>
    ApproveAlways,

    /// <summary>Do not run it. The model is told, and can propose something else.</summary>
    Deny,
}

/// <summary>
/// Asks the user before a tool changes anything.
/// </summary>
/// <remarks>
/// The agent loop consults this for every tool the server has not marked read-only. A language
/// model silently rewriting someone's report is not acceptable, whatever the backups say.
/// </remarks>
public interface IToolApprovalService
{
    /// <summary>
    /// Asks whether a tool may run.
    /// </summary>
    /// <param name="request">What the model wants to do.</param>
    /// <param name="cancellationToken">Cancels the prompt.</param>
    /// <returns>The user's decision.</returns>
    ValueTask<ToolApprovalDecision> RequestAsync(ToolApprovalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Refuses every mutating tool.
/// </summary>
/// <remarks>
/// The default when no interactive approver has been wired up. Failing closed means a
/// half-configured host degrades to a read-only assistant rather than an unsupervised one.
/// </remarks>
public sealed class DenyAllToolApprovalService : IToolApprovalService
{
    /// <summary>The shared instance.</summary>
    public static DenyAllToolApprovalService Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<ToolApprovalDecision> RequestAsync(
        ToolApprovalRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(ToolApprovalDecision.Deny);
}

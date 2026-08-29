using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportExpert.Modules.Copilot.Services;

namespace ReportExpert.Modules.Copilot.ViewModels;

/// <summary>
/// Anything that appears in the conversation.
/// </summary>
public abstract partial class CopilotMessageViewModel : ObservableObject
{
    /// <summary>Whether the user wrote it, which decides which side it sits on.</summary>
    public bool IsUser { get; init; }
}

/// <summary>A plain message from the user or the assistant.</summary>
public partial class ChatMessageViewModel : CopilotMessageViewModel
{
    /// <summary>Who wrote it.</summary>
    public string Author => IsUser ? "You" : "Rex";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking))]
    [NotifyPropertyChangedFor(nameof(ShowMarkdown))]
    private string _content = string.Empty;

    /// <summary>True while Rex is composing a reply (placeholder content).</summary>
    public bool IsThinking =>
        !IsUser && Content is "Thinking..." or "Thinking…";

    /// <summary>Whether the markdown body should be shown (hide while thinking).</summary>
    public bool ShowMarkdown => !IsThinking;
}

/// <summary>Where a tool call has got to.</summary>
public enum ToolCallStatus
{
    /// <summary>Waiting for the user to allow or refuse it.</summary>
    AwaitingApproval,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Finished successfully.</summary>
    Succeeded,

    /// <summary>The tool reported a failure.</summary>
    Failed,

    /// <summary>The user refused it.</summary>
    Denied,

    /// <summary>The tool is turned off in Settings.</summary>
    Disabled,
}

/// <summary>
/// A card in the conversation showing one tool call: what it is, what it was asked to do, and how
/// it went.
/// </summary>
/// <remarks>
/// While a mutating call is awaiting approval the card is also the prompt, so the user decides in
/// context with the arguments in front of them rather than in a modal dialog that has lost the
/// thread of the conversation.
/// </remarks>
public partial class ToolCallMessageViewModel : CopilotMessageViewModel
{
    private readonly TaskCompletionSource<ToolApprovalDecision> _decision =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The model's identifier for this call.</summary>
    public required string CallId { get; init; }

    /// <summary>The tool's name, as the user should see it.</summary>
    public required string ToolName { get; init; }

    /// <summary>Whether the tool would change the report.</summary>
    public bool IsMutating { get; init; }

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAwaitingApproval))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(IsSucceeded))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    [NotifyPropertyChangedFor(nameof(IsDenied))]
    [NotifyPropertyChangedFor(nameof(IsDisabled))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ToolCallStatus _status = ToolCallStatus.Running;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSummary))]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _wasAutoApproved;

    /// <summary>Whether there is an outcome worth showing under the tool name.</summary>
    public bool HasSummary => Summary.Length > 0;

    /// <summary>Whether the approval buttons should be shown.</summary>
    public bool IsAwaitingApproval => Status == ToolCallStatus.AwaitingApproval;

    /// <summary>Whether to show the spinner.</summary>
    public bool IsRunning => Status == ToolCallStatus.Running;

    /// <summary>Whether the call finished successfully (verified).</summary>
    public bool IsSucceeded => Status == ToolCallStatus.Succeeded;

    /// <summary>Whether the tool reported failure.</summary>
    public bool IsFailed => Status == ToolCallStatus.Failed;

    /// <summary>Whether the user declined.</summary>
    public bool IsDenied => Status == ToolCallStatus.Denied;

    /// <summary>Whether the tool is turned off in Settings.</summary>
    public bool IsDisabled => Status == ToolCallStatus.Disabled;

    /// <summary>A short caption for the current state.</summary>
    public string StatusText => Status switch
    {
        ToolCallStatus.AwaitingApproval => IsMutating ? "Confirm this change?" : "Waiting for approval",
        ToolCallStatus.Running => WasAutoApproved ? "Applying (auto-approved)…" : "Applying…",
        ToolCallStatus.Succeeded => WasAutoApproved ? "Verified · auto-approved" : "Verified",
        ToolCallStatus.Failed => "Failed",
        ToolCallStatus.Denied => "Declined",
        ToolCallStatus.Disabled => "Disabled — enable in Settings",
        _ => string.Empty,
    };

    /// <summary>Completes once the user has decided, or immediately if no decision is needed.</summary>
    public Task<ToolApprovalDecision> Decision => _decision.Task;

    [RelayCommand]
    private void ApproveOnce() => Decide(ToolApprovalDecision.Approve);

    [RelayCommand]
    private void ApproveAlways() => Decide(ToolApprovalDecision.ApproveAlways);

    [RelayCommand]
    private void Deny() => Decide(ToolApprovalDecision.Deny);

    [RelayCommand]
    private void ToggleDetails() => IsExpanded = !IsExpanded;

    /// <summary>
    /// Settles the decision without user input, used when a turn is cancelled while a card is
    /// still waiting.
    /// </summary>
    /// <param name="decision">What to record.</param>
    public void Decide(ToolApprovalDecision decision)
    {
        if (!_decision.TrySetResult(decision))
            return;

        if (decision == ToolApprovalDecision.ApproveAlways)
            WasAutoApproved = true;

        Status = decision == ToolApprovalDecision.Deny ? ToolCallStatus.Denied : ToolCallStatus.Running;
    }
}

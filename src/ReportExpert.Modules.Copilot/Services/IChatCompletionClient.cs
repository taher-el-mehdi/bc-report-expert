using System.Text.Json;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>
/// A request from the model to run a tool.
/// </summary>
/// <param name="Id">
/// The provider's identifier for this call. The matching result must quote it back, or the model
/// cannot tell which answer belongs to which call.
/// </param>
/// <param name="Name">The tool name, qualified by the server that offers it.</param>
/// <param name="ArgumentsJson">The arguments, as the JSON object the model produced.</param>
public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>
/// One message in a conversation.
/// </summary>
/// <param name="Role">
/// <c>system</c>, <c>user</c>, <c>assistant</c> or <c>tool</c>.
/// </param>
/// <param name="Content">The message text. Empty on an assistant turn that only calls tools.</param>
public sealed record ChatMessage(string Role, string Content)
{
    /// <summary>
    /// On a <c>tool</c> message, which call this is the result of.
    /// </summary>
    public string? ToolCallId { get; init; }

    /// <summary>On a <c>tool</c> message, the name of the tool that ran.</summary>
    public string? Name { get; init; }

    /// <summary>On an <c>assistant</c> message, the tools the model asked to run.</summary>
    public IReadOnlyList<ChatToolCall>? ToolCalls { get; init; }

    /// <summary>Builds the result message for a completed tool call.</summary>
    /// <param name="toolCallId">The identifier from the call being answered.</param>
    /// <param name="name">The tool that ran.</param>
    /// <param name="content">What it returned.</param>
    /// <returns>The message to append to the conversation.</returns>
    public static ChatMessage ForToolResult(string toolCallId, string name, string content) =>
        new("tool", content) { ToolCallId = toolCallId, Name = name };
}

/// <summary>
/// A tool offered to the model, in the shape the chat completions API expects.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">What it does, written for the model.</param>
/// <param name="ParametersSchema">JSON Schema describing the arguments.</param>
public sealed record ChatTool(string Name, string Description, JsonElement ParametersSchema);

/// <summary>
/// What the model returned for one turn.
/// </summary>
/// <param name="Content">The assistant's text, which may be empty when it only called tools.</param>
/// <param name="ToolCalls">The tools it asked to run, empty when it just replied.</param>
public sealed record ChatCompletion(string Content, IReadOnlyList<ChatToolCall> ToolCalls)
{
    /// <summary>Whether the model wants tools run before it can answer.</summary>
    public bool WantsTools => ToolCalls.Count > 0;
}

/// <summary>
/// One turn's worth of input.
/// </summary>
/// <param name="Messages">The conversation so far.</param>
public sealed record ChatCompletionRequest(IReadOnlyList<ChatMessage> Messages)
{
    /// <summary>
    /// The tools the model may call. Empty means a plain chat turn; no <c>tools</c> array is sent,
    /// which matters because some models behave differently when one is present.
    /// </summary>
    public IReadOnlyList<ChatTool> Tools { get; init; } = [];

    /// <summary>How freely the model may pick a tool. <c>auto</c> unless there is reason to force one.</summary>
    public string ToolChoice { get; init; } = "auto";
}

/// <summary>OpenAI-compatible chat completion client.</summary>
public interface IChatCompletionClient
{
    /// <summary>
    /// Sends a plain chat turn and returns the reply text.
    /// </summary>
    /// <param name="messages">The conversation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The assistant's reply.</returns>
    Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a turn that may result in tool calls.
    /// </summary>
    /// <param name="request">The conversation and the tools on offer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reply, plus any tools the model wants run.</returns>
    Task<ChatCompletion> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}

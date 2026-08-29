using System.Text.Json;
using ReportExpert.Mcp.Client;

namespace ReportExpert.Modules.Copilot.Services;

/// <summary>
/// Runs one assistant turn: ask the model, run the tools it asks for, feed the results back, and
/// repeat until it answers in prose.
/// </summary>
/// <remarks>
/// <para>
/// The loop is bounded. A model that keeps calling tools without converging would otherwise spend
/// the user's money indefinitely, so it is stopped after a fixed number of rounds and asked to
/// summarize what it has.
/// </para>
/// <para>
/// Read-only tools run without asking. Everything else goes through
/// <see cref="IToolApprovalService"/> first.
/// </para>
/// </remarks>
public sealed class RexAgentLoop
{
    /// <summary>
    /// How much of a tool result is passed back to the model.
    /// </summary>
    /// <remarks>
    /// A dataset listing on a large report can run to tens of thousands of characters, which would
    /// crowd out the conversation. The tools all support narrowing, so a truncated result is a
    /// prompt to ask more precisely rather than a loss.
    /// </remarks>
    private const int MaxToolResultChars = 8_000;

    private static readonly JsonSerializerOptions DisplayOptions = new() { WriteIndented = true };

    private readonly IChatCompletionClient _client;
    private readonly IMcpToolGateway _gateway;
    private readonly IToolApprovalService _approval;

    /// <summary>Creates a loop.</summary>
    /// <param name="client">The chat backend.</param>
    /// <param name="gateway">The tools available.</param>
    /// <param name="approval">Who to ask before a tool changes anything.</param>
    public RexAgentLoop(IChatCompletionClient client, IMcpToolGateway gateway, IToolApprovalService? approval = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(gateway);

        _client = client;
        _gateway = gateway;
        _approval = approval ?? DenyAllToolApprovalService.Instance;
    }

    /// <summary>How many times the model may call tools before the loop gives up. </summary>
    public int MaxIterations { get; init; } = 8;

    /// <summary>
    /// Runs a turn.
    /// </summary>
    /// <param name="conversation">The conversation so far, ending with the user's message.</param>
    /// <param name="progress">Where to report tool activity, for the UI.</param>
    /// <param name="cancellationToken">Stops the turn.</param>
    /// <returns>The assistant's reply and what it changed.</returns>
    public async Task<AgentTurnResult> RunAsync(
        IReadOnlyList<ChatMessage> conversation,
        IProgress<AgentEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var allTools = _gateway is IFilteredMcpToolGateway filtered
            ? await filtered.ListAllToolsAsync(cancellationToken).ConfigureAwait(false)
            : await _gateway.ListToolsAsync(cancellationToken).ConfigureAwait(false);

        var tools = allTools
            .Where(tool => _gateway is not IFilteredMcpToolGateway gate || gate.IsToolEnabled(tool.QualifiedName))
            .ToList();

        var byCallName = BuildCallNameIndex(tools);
        var offered = tools.Select(tool => ToChatTool(tool, byCallName)).ToList();
        var allByCallName = BuildCallNameIndex(allTools);

        var messages = new List<ChatMessage>(conversation);
        var disabledTools = allTools
            .Where(tool => tools.All(enabled =>
                !string.Equals(enabled.QualifiedName, tool.QualifiedName, StringComparison.Ordinal)))
            .ToList();

        if (disabledTools.Count > 0)
        {
            string names = string.Join(", ", disabledTools.Select(tool => tool.ToolName).OrderBy(n => n, StringComparer.Ordinal));
            messages.Insert(0, new ChatMessage(
                "system",
                "The user has disabled these tools in Settings → Agent mode → Tool servers: " +
                names +
                ". If they ask you to do something that needs one of them, tell them they have to enable " +
                "that tool there before you can perform it. Do not invent a substitute call."));
        }

        var changedFiles = new List<string>();

        bool approvedForTurn = false;
        int toolCallCount = 0;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var completion = await _client
                .CompleteAsync(new ChatCompletionRequest(messages) { Tools = offered }, cancellationToken)
                .ConfigureAwait(false);

            if (!completion.WantsTools)
                return new AgentTurnResult(completion.Content, toolCallCount, changedFiles, StoppedAtLimit: false);

            messages.Add(new ChatMessage("assistant", completion.Content) { ToolCalls = completion.ToolCalls });

            foreach (var call in completion.ToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byCallName.TryGetValue(call.Name, out var descriptor);

                // Groq/Llama often drops the server prefix or invents a bare name. Resolve to the
                // real descriptor before giving up, so a near-miss still runs.
                descriptor ??= ResolveLooseName(call.Name, tools);

                // The model may still ask for a tool the user turned off (e.g. from an earlier
                // turn). Resolve against the full catalogue so we can say "enable it" rather than
                // "no such tool".
                var disabledDescriptor = descriptor is null
                    ? ResolveDisabledTool(call.Name, allTools, tools, allByCallName)
                    : null;

                if (descriptor is null && disabledDescriptor is not null)
                {
                    string disabledName = disabledDescriptor.ToolName;
                    string disabledCallName = ModelFacingName(disabledDescriptor, allByCallName);
                    string disabledMessage = FilteringMcpToolGateway.DisabledToolMessage(disabledName);

                    progress?.Report(new AgentEvent.ToolStarted(call.Id, disabledName, Prettify(call.ArgumentsJson)));
                    progress?.Report(new AgentEvent.ToolDisabled(call.Id, disabledName, disabledMessage));

                    messages.Add(ChatMessage.ForToolResult(call.Id, disabledCallName, disabledMessage));
                    continue;
                }

                string callName = descriptor is null
                    ? call.Name
                    : ModelFacingName(descriptor, byCallName);
                string displayName = descriptor?.ToolName ?? call.Name;
                string arguments = Prettify(call.ArgumentsJson);

                if (descriptor is null)
                {
                    // The model hallucinated a tool. Telling it so is more useful than failing the
                    // turn, because it will usually pick a real one on the next round.
                    messages.Add(ChatMessage.ForToolResult(
                        call.Id,
                        call.Name,
                        $"There is no tool called '{call.Name}'. Use one of the tools you were given, " +
                        "with the exact name from the tools list."));
                    continue;
                }

                if (_gateway is IFilteredMcpToolGateway enablement &&
                    !enablement.IsToolEnabled(descriptor.QualifiedName))
                {
                    string disabledMessage = FilteringMcpToolGateway.DisabledToolMessage(displayName);

                    progress?.Report(new AgentEvent.ToolStarted(call.Id, displayName, arguments));
                    progress?.Report(new AgentEvent.ToolDisabled(call.Id, displayName, disabledMessage));

                    messages.Add(ChatMessage.ForToolResult(call.Id, callName, disabledMessage));
                    continue;
                }

                if (!descriptor.IsReadOnly && !approvedForTurn)
                {
                    var decision = await _approval
                        .RequestAsync(
                            new ToolApprovalRequest(call.Id, callName, displayName, arguments, descriptor.IsDestructive),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (decision == ToolApprovalDecision.Deny)
                    {
                        progress?.Report(new AgentEvent.ToolDenied(call.Id, displayName));

                        messages.Add(ChatMessage.ForToolResult(
                            call.Id,
                            callName,
                            "The user declined this change. Do not retry it. Explain what you were going to do, " +
                            "or suggest a different approach."));

                        continue;
                    }

                    approvedForTurn = decision is ToolApprovalDecision.ApproveForTurn
                        or ToolApprovalDecision.ApproveAlways;
                }

                progress?.Report(new AgentEvent.ToolStarted(call.Id, displayName, arguments));

                var result = await _gateway
                    .CallToolAsync(descriptor.QualifiedName, ParseArguments(call.ArgumentsJson), cancellationToken)
                    .ConfigureAwait(false);

                toolCallCount++;

                progress?.Report(new AgentEvent.ToolFinished(call.Id, result.IsError, Summarize(result)));

                if (!descriptor.IsReadOnly && !result.IsError && TryGetChangedFile(call.ArgumentsJson, out string path))
                {
                    if (!changedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                        changedFiles.Add(path);

                    progress?.Report(new AgentEvent.FileChanged(path));
                }

                messages.Add(ChatMessage.ForToolResult(call.Id, callName, Truncate(result.Text)));
            }
        }

        // Out of rounds. One more call with no tools on offer forces a prose answer, so the user
        // gets something rather than silence.
        var wrapUp = await _client
            .CompleteAsync(
                new ChatCompletionRequest([.. messages, new ChatMessage("system", WrapUpInstruction)]),
                cancellationToken)
            .ConfigureAwait(false);

        return new AgentTurnResult(wrapUp.Content, toolCallCount, changedFiles, StoppedAtLimit: true);
    }

    private const string WrapUpInstruction =
        "You have used the maximum number of tool calls for this turn. Do not request any more. " +
        "Summarize what you found and what you changed, and say what remains to be done.";

    /// <summary>
    /// The name the model should call a tool by: the bare MCP name when it is unique across
    /// servers, otherwise the qualified <c>server__tool</c> form.
    /// </summary>
    /// <remarks>
    /// Groq and Llama routinely drop a <c>rdl__</c> prefix and call the bare name. Advertising
    /// bare names when safe avoids a 400 from Groq's tool-name validator.
    /// </remarks>
    private static string ModelFacingName(
        McpToolDescriptor descriptor,
        IReadOnlyDictionary<string, McpToolDescriptor> byCallName) =>
        byCallName.ContainsKey(descriptor.ToolName)
            ? descriptor.ToolName
            : descriptor.QualifiedName;

    private static ChatTool ToChatTool(
        McpToolDescriptor descriptor,
        IReadOnlyDictionary<string, McpToolDescriptor> byCallName) =>
        new(ModelFacingName(descriptor, byCallName), descriptor.Description, descriptor.JsonSchema);

    /// <summary>
    /// Indexes tools under every name the model might call them by.
    /// </summary>
    private static Dictionary<string, McpToolDescriptor> BuildCallNameIndex(
        IReadOnlyList<McpToolDescriptor> tools)
    {
        var index = new Dictionary<string, McpToolDescriptor>(StringComparer.Ordinal);

        foreach (var tool in tools)
            index[tool.QualifiedName] = tool;

        foreach (var group in tools.GroupBy(tool => tool.ToolName, StringComparer.Ordinal))
        {
            if (group.Count() == 1)
                index[group.Key] = group.First();
        }

        return index;
    }

    /// <summary>
    /// Matches a model-supplied name that is close to a real tool (prefix stripped, case, etc.).
    /// </summary>
    private static McpToolDescriptor? ResolveLooseName(string name, IReadOnlyList<McpToolDescriptor> tools)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var exactBare = tools.Where(tool =>
                string.Equals(tool.ToolName, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactBare.Count == 1)
            return exactBare[0];

        // "rdl__set_report_item_visibility" or a mangled "rdl_set_report_item_visibility"
        string trimmed = name;
        int separator = name.IndexOf(McpToolRegistry.NameSeparator, StringComparison.Ordinal);
        if (separator > 0 && separator + McpToolRegistry.NameSeparator.Length < name.Length)
            trimmed = name[(separator + McpToolRegistry.NameSeparator.Length)..];
        else if (name.Contains('_', StringComparison.Ordinal))
        {
            // Single-underscore prefix mistake: rdl_set_report_item_visibility
            int first = name.IndexOf('_');
            if (first > 0 && first + 1 < name.Length)
            {
                var candidate = tools.Where(tool =>
                        string.Equals(tool.ToolName, name[(first + 1)..], StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (candidate.Count == 1)
                    return candidate[0];
            }
        }

        var byTrimmed = tools.Where(tool =>
                string.Equals(tool.ToolName, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return byTrimmed.Count == 1 ? byTrimmed[0] : null;
    }

    /// <summary>
    /// Finds a tool the model asked for that exists but is currently disabled.
    /// </summary>
    private static McpToolDescriptor? ResolveDisabledTool(
        string name,
        IReadOnlyList<McpToolDescriptor> allTools,
        IReadOnlyList<McpToolDescriptor> enabledTools,
        IReadOnlyDictionary<string, McpToolDescriptor> allByCallName)
    {
        McpToolDescriptor? candidate = allByCallName.TryGetValue(name, out var exact)
            ? exact
            : ResolveLooseName(name, allTools);

        if (candidate is null)
            return null;

        bool isEnabled = enabledTools.Any(tool =>
            string.Equals(tool.QualifiedName, candidate.QualifiedName, StringComparison.Ordinal));

        return isEnabled ? null : candidate;
    }

    private static IReadOnlyDictionary<string, object?>? ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var property in document.RootElement.EnumerateObject())
                arguments[property.Name] = ToClrValue(property.Value);

            return arguments;
        }
        catch (JsonException)
        {
            // A model occasionally emits malformed arguments. Passing none through lets the server
            // reject the call with a schema error the model can read and correct.
            return null;
        }
    }

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Number => element.TryGetInt64(out long whole) ? whole : element.GetDouble(),
        _ => element.Clone(),
    };

    private static bool TryGetChangedFile(string argumentsJson, out string path)
    {
        path = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("filepath", out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                path = element.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // No path to report; the caller simply will not trigger a reload.
        }

        return path.Length > 0;
    }

    private static string Summarize(McpToolResult result)
    {
        // The RDLC server answers with an envelope carrying a message on success and a message
        // plus a hint on failure. Either is a better card caption than raw JSON.
        try
        {
            using var document = JsonDocument.Parse(result.Text);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return Truncate(result.Text, 200);

            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var errorMessage))
            {
                return errorMessage.GetString() ?? "Failed.";
            }

            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Done.";
            }

            return result.IsError ? "Failed." : "Done.";
        }
        catch (JsonException)
        {
            return Truncate(result.Text, 200);
        }
    }

    private static string Prettify(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, DisplayOptions);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string Truncate(string text, int maxChars = MaxToolResultChars) =>
        text.Length <= maxChars
            ? text
            : text[..maxChars] + $"\n… truncated, {text.Length - maxChars} more characters. Narrow the request to see the rest.";
}

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReportExpert.Mcp.Client;
using ReportExpert.Modules.Copilot.Models;
using ReportExpert.Modules.Copilot.Services;

namespace ReportExpert.Modules.Copilot.ViewModels;

/// <summary>
/// Chat view model for the Copilot module.
/// </summary>
/// <remarks>
/// Runs in one of two modes. Without a connected MCP gateway, or with agent mode switched off, it
/// explains the report and suggests changes for the user to make. With both, it can call tools to
/// inspect and edit the report, asking before anything is written.
/// </remarks>
public partial class CopilotViewModel : ObservableObject, IToolApprovalService
{
    private const int MaxHistoryMessages = 6;
    private const int MaxHistoryMessageChars = 1_500;

    private const string BaseSystemPrompt =
        """
        You are Rex, Report Expert's assistant specialized in Microsoft RDLC (Report Definition Language Client-side)
        reports, as used by Microsoft Dynamics 365 Business Central / NAV.

        Your role is to help the user understand the current RDLC report layout.
        You may explain structure, datasets, parameters, expressions, and layout regions.
        You may suggest ideas or show example XML snippets the user can copy manually.

        You receive a compact SUMMARY of the report (datasets, fields, parameters, item names) — not the full XML.
        Answer from that summary. If something is missing from the summary, say what is known and what you cannot see.

        Rules:
        - Do not modify files, apply patches, call tools, or claim you can auto-fix anything.
        - Do not invent tool calls, rdlc-patch blocks, or agent workflows.
        - When you suggest a change, show a short before/after XML snippet the user can apply themselves in the designer.
        - Be concise and practical. Prefer short explanations over long dumps.
        - If no report is loaded, say so and answer generally about RDLC.
        """;

    private const string AgentSystemPrompt =
        """
        You are Rex, the Report Expert assistant. You work on Microsoft RDLC reports, as used by
        Microsoft Dynamics 365 Business Central / NAV, and you have tools that read and edit them.

        How to work:
        - Every tool takes a filepath. Use the path of the report given below unless the user names another.
        - Call only tools from the tools list, using each tool's exact name. Do not invent tool names
          and do not add or remove a server prefix.
        - Do not guess at a report's contents. Use the read tools (describe, list items/columns/
          datasets, find usages, validate) to find out. They are cheap and always current.
        - To hide a layout item such as a Textbox or Image: look up its exact Name with the report-
          items tool, then call the visibility tool with hidden=true. Prefer hiding over deleting
          when the item sits inside a table cell.
        - Column indexes come from the columns tool. Never assume one.
        - Before binding a column to a field, make sure the dataset declares that field; add it if not.
        - Before renaming or removing a field, find its usages first.
        - After a series of edits, validate the report.
        - Every tool returns {ok, data, error}. When ok is false, read error.hint: it says what to do next.

        Confirmation and chat style:
        - NEVER ask the user to type "confirm", "please confirm", or "are you sure" in chat.
          Mutating tools show Confirm once / Confirm always buttons in the UI — that is the only approval.
        - NEVER paste or restate the internal RDLC context (datasets, fields, parameters, item lists)
          into your replies. The user already has the report open; a short sentence is enough.
        - NEVER claim you changed the report unless a mutating tool returned ok=true.
        - If the named item does not exist, say so and stop — do not invent a change.
        - Be concise. One or two sentences on what you did or what you need. No dump of the report.
        """;

    private static readonly string DefaultWelcome =
        "Hi! I'm **Rex**. Ask me about this RDLC layout — structure, datasets, expressions, or how something works.\n\n" +
        "I can explain and suggest ideas. I won't change the file for you.";

    private static readonly string AgentWelcome =
        "Hi! I'm **Rex**. Ask me about this RDLC layout, or ask me to change it.\n\n" +
        "- Rename or reformat columns?\n" +
        "- Hide or remove report items?\n" +
        "- Fix datasets, fields, or expressions?\n\n" +
        "Edits appear as cards — **Confirm once**, **Confirm always**, or **Decline**. I take a backup first.";

    private readonly CopilotSettingsService _settingsService = new();
    private readonly CopilotChatHistoryStore _historyStore = new();
    private readonly IChatCompletionClient _client;
    private readonly Dictionary<string, ToolCallMessageViewModel> _toolCards = new(StringComparer.Ordinal);

    private CancellationTokenSource? _turn;
    private string _reportPath = string.Empty;
    private string? _cachedSummary;
    private string? _cachedSummaryPath;
    private bool _autoApproveSession;
    private bool _loadingSettings;

    public CopilotSettingsService CopilotSettingsService => _settingsService;

    public ObservableCollection<CopilotMessageViewModel> Messages { get; } = [];

    /// <summary>Models for the current provider — selectable from the chat footer.</summary>
    public ObservableCollection<AiModelOption> ModelOptions { get; } = [];

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyStatusText))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyStatusText))]
    private bool _isApplyingLayoutChanges;

    /// <summary>Caption under the busy indicator while Rex is working.</summary>
    public string BusyStatusText =>
        IsApplyingLayoutChanges ? "Rex is updating the layout…" : "Rex is working…";

    [ObservableProperty]
    private string _reportContextDisplay = "No report loaded";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _baseUrl = string.Empty;

    [ObservableProperty]
    private string _azureDeployment = string.Empty;

    [ObservableProperty]
    private string _selectedProvider = nameof(CopilotProvider.Groq);

    [ObservableProperty]
    private string _providerDisplayName = CopilotProviderDefaults.DisplayName(CopilotProvider.Groq);

    public bool HasApiKey =>
        !string.IsNullOrWhiteSpace(_settingsService.Settings.ApiKey);

    /// <summary>
    /// The MCP tools available, supplied by the shell. Null until a gateway has connected, and
    /// deliberately typed as the abstraction so this view model knows nothing about which servers
    /// exist.
    /// </summary>
    public IMcpToolGateway? ToolGateway { get; private set; }

    /// <summary>
    /// Whether the assistant will use tools: the user has to have turned agent mode on, and a
    /// gateway has to be connected.
    /// </summary>
    public bool IsAgentMode => ToolGateway is not null && _settingsService.Settings.AgentModeEnabled;

    /// <summary>Shell wires this to read the current report XML from disk.</summary>
    public Func<Task<string>>? GetCurrentXmlAsync { get; set; }

    /// <summary>
    /// Shell wires this to reload a report the assistant has just edited.
    /// </summary>
    /// <remarks>
    /// Without it, an editor holding the report in memory would overwrite the assistant's work the
    /// next time the user saved.
    /// </remarks>
    public Func<string, Task>? ReloadReportAsync { get; set; }

    /// <summary>
    /// Shell wires this so the layout designer can show a sparkle overlay while Rex is writing.
    /// </summary>
    public Action<bool>? AgentEditingChanged { get; set; }

    public CopilotViewModel()
    {
        _client = new OpenAiCompatibleClient(_settingsService);
        LoadSettingsIntoUi();
        ShowWelcome();
    }

    /// <summary>
    /// Supplies the tool gateway, or clears it when the servers shut down.
    /// Disabled tools are filtered using the current Copilot settings.
    /// </summary>
    /// <param name="gateway">The gateway, or <see langword="null"/>.</param>
    public void SetToolGateway(IMcpToolGateway? gateway)
    {
        ToolGateway = gateway is null
            ? null
            : new FilteringMcpToolGateway(
                gateway,
                qualifiedName => _settingsService.Settings.IsMcpToolEnabled(qualifiedName));
        OnPropertyChanged(nameof(IsAgentMode));
    }

    public void SetWorkspaceRoot(string? rootPath) =>
        _historyStore.SetWorkspaceRoot(rootPath);

    /// <summary>Called by the shell when an RDL or RDLC layout is opened on Home.</summary>
    public void SetReportContext(string path)
    {
        if (!string.IsNullOrWhiteSpace(_reportPath) &&
            !string.Equals(_reportPath, path, StringComparison.OrdinalIgnoreCase))
            PersistCurrentHistory();

        _reportPath = path ?? string.Empty;
        _cachedSummary = null;
        _cachedSummaryPath = null;
        string name = Path.GetFileName(_reportPath);
        string ext = Path.GetExtension(_reportPath).TrimStart('.').ToUpperInvariant();
        ReportContextDisplay = string.IsNullOrWhiteSpace(name)
            ? "No file selected"
            : $"{ext} · {name}";

        LoadHistoryForCurrentFile();
    }

    public void ClearReportContext()
    {
        PersistCurrentHistory();
        _reportPath = string.Empty;
        _cachedSummary = null;
        _cachedSummaryPath = null;
        ReportContextDisplay = "No file selected";
        Messages.Clear();
        ShowWelcome();
    }

    [RelayCommand]
    private Task SendAsync() => SendMessageAsync(InputText);

    /// <summary>Stops the turn in progress.</summary>
    [RelayCommand]
    private void Stop() => _turn?.Cancel();

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        _toolCards.Clear();
        ShowWelcome();

        if (!string.IsNullOrWhiteSpace(_reportPath))
            _historyStore.Delete(_reportPath);
    }

    public void ReloadSettings()
    {
        LoadSettingsIntoUi();
        OnPropertyChanged(nameof(HasApiKey));
        OnPropertyChanged(nameof(IsAgentMode));

        // Turning agent mode off clears "Confirm always" so the next session asks again.
        if (!_settingsService.Settings.AgentModeEnabled)
            _autoApproveSession = false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Approval happens inline in the conversation: a card appears with the arguments and three
    /// buttons, and this waits on it. That keeps the decision next to the context it needs.
    /// The agent loop often resumes on a thread-pool thread after an HTTP call, so UI work is
    /// always marshalled onto the dispatcher.
    /// </remarks>
    async ValueTask<ToolApprovalDecision> IToolApprovalService.RequestAsync(
        ToolApprovalRequest request,
        CancellationToken cancellationToken)
    {
        return await InvokeOnUiAsync(() => RequestApprovalOnUiAsync(request, cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<ToolApprovalDecision> RequestApprovalOnUiAsync(
        ToolApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var card = new ToolCallMessageViewModel
        {
            CallId = request.CallId,
            ToolName = request.DisplayName,
            IsMutating = true,
            Arguments = request.ArgumentsJson,
            Status = ToolCallStatus.AwaitingApproval,
        };

        _toolCards[request.CallId] = card;
        Messages.Add(card);

        if (_autoApproveSession)
        {
            card.WasAutoApproved = true;
            card.Decide(ToolApprovalDecision.Approve);
            return ToolApprovalDecision.Approve;
        }

        using var registration = cancellationToken.Register(() =>
            _ = InvokeOnUiAsync(() =>
            {
                card.Decide(ToolApprovalDecision.Deny);
                return Task.CompletedTask;
            }));

        var decision = await card.Decision.ConfigureAwait(true);

        if (decision == ToolApprovalDecision.ApproveAlways)
        {
            _autoApproveSession = true;
            return ToolApprovalDecision.ApproveForTurn;
        }

        return decision;
    }

    partial void OnIsApplyingLayoutChangesChanged(bool value) =>
        _ = InvokeOnUiAsync(() =>
        {
            AgentEditingChanged?.Invoke(value);
            return Task.CompletedTask;
        });

    private async Task SendMessageAsync(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || IsBusy)
            return;

        InputText = string.Empty;
        Messages.Add(new ChatMessageViewModel { IsUser = true, Content = text });

        var reply = new ChatMessageViewModel { IsUser = false, Content = "Thinking..." };
        Messages.Add(reply);
        IsBusy = true;

        _turn?.Dispose();
        _turn = new CancellationTokenSource();

        try
        {
            var history = BuildHistory(text);

            string answer = IsAgentMode
                ? await RunAgentTurnAsync(history, _turn.Token).ConfigureAwait(true)
                : await _client.CompleteAsync(history, _turn.Token).ConfigureAwait(true);

            await InvokeOnUiAsync(() =>
            {
                reply.Content = string.IsNullOrWhiteSpace(answer) ? "(empty response)" : answer.Trim();
                return Task.CompletedTask;
            }).ConfigureAwait(true);

            PersistCurrentHistory();
        }
        catch (OperationCanceledException)
        {
            await InvokeOnUiAsync(() =>
            {
                reply.Content = "Stopped.";
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await InvokeOnUiAsync(() =>
            {
                reply.Content = $"Error: {ex.Message}";
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        }
        finally
        {
            await InvokeOnUiAsync(() =>
            {
                SettlePendingApprovals();
                IsBusy = false;
                IsApplyingLayoutChanges = false;
                return Task.CompletedTask;
            }).ConfigureAwait(true);
        }
    }

    private async Task<string> RunAgentTurnAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken)
    {
        var loop = new RexAgentLoop(_client, ToolGateway!, this);

        // Progress<T> captures the UI context here, so the handler runs on the dispatcher and can
        // touch the Messages collection directly.
        var progress = new Progress<AgentEvent>(OnAgentEvent);

        var result = await loop.RunAsync(history, progress, cancellationToken).ConfigureAwait(true);

        foreach (string path in result.ChangedFiles)
        {
            if (ReloadReportAsync is { } reload)
                await reload(path).ConfigureAwait(true);
        }

        if (!result.StoppedAtLimit)
            return result.Content;

        return string.IsNullOrWhiteSpace(result.Content)
            ? "I ran out of steps for this request. Ask me to continue and I'll pick up where I left off."
            : result.Content + "\n\n(I ran out of steps for this request. Ask me to continue.)";
    }

    private void OnAgentEvent(AgentEvent update)
    {
        // Progress<T> normally posts here on the UI thread; marshal defensively if not.
        if (Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ApplyAgentEvent(update));
            return;
        }

        ApplyAgentEvent(update);
    }

    private void ApplyAgentEvent(AgentEvent update)
    {
        switch (update)
        {
            case AgentEvent.ToolStarted started:
            {
                if (_toolCards.TryGetValue(started.CallId, out var existing))
                {
                    existing.Status = ToolCallStatus.Running;
                    if (existing.IsMutating)
                        IsApplyingLayoutChanges = true;
                    break;
                }

                var card = new ToolCallMessageViewModel
                {
                    CallId = started.CallId,
                    ToolName = started.ToolName,
                    Arguments = started.ArgumentsJson,
                    Status = ToolCallStatus.Running,
                };

                _toolCards[started.CallId] = card;
                Messages.Add(card);
                break;
            }

            case AgentEvent.ToolFinished finished when _toolCards.TryGetValue(finished.CallId, out var card):
                card.Status = finished.IsError ? ToolCallStatus.Failed : ToolCallStatus.Succeeded;
                card.Summary = finished.Summary;
                if (!_toolCards.Values.Any(c => c.Status == ToolCallStatus.Running && c.IsMutating))
                    IsApplyingLayoutChanges = false;
                break;

            case AgentEvent.ToolDenied denied when _toolCards.TryGetValue(denied.CallId, out var card):
                card.Status = ToolCallStatus.Denied;
                break;

            case AgentEvent.ToolDisabled disabled:
            {
                if (!_toolCards.TryGetValue(disabled.CallId, out var card))
                {
                    card = new ToolCallMessageViewModel
                    {
                        CallId = disabled.CallId,
                        ToolName = disabled.ToolName,
                        Status = ToolCallStatus.Disabled,
                        Summary = disabled.Message,
                    };
                    _toolCards[disabled.CallId] = card;
                    Messages.Add(card);
                    break;
                }

                card.Status = ToolCallStatus.Disabled;
                card.Summary = disabled.Message;
                break;
            }

            case AgentEvent.FileChanged changed
                when string.Equals(changed.FilePath, _reportPath, StringComparison.OrdinalIgnoreCase):
                // The summary describes a file that has just been rewritten.
                _cachedSummary = null;
                _cachedSummaryPath = null;
                IsApplyingLayoutChanges = true;
                break;
        }
    }

    /// <summary>The WPF dispatcher that owns the chat UI, when one is available.</summary>
    private static Dispatcher? Dispatcher => Application.Current?.Dispatcher;

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread. The agent loop uses
    /// <c>ConfigureAwait(false)</c> after HTTP calls, so approval and chat updates must hop back.
    /// </summary>
    private static Task<T> InvokeOnUiAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            return action();

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private static Task InvokeOnUiAsync(Func<Task> action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            return action();

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    /// <summary>
    /// Releases any card still waiting on a decision, so a cancelled turn cannot leave the loop
    /// blocked on a prompt nobody will answer.
    /// </summary>
    private void SettlePendingApprovals()
    {
        foreach (var card in _toolCards.Values)
        {
            if (card.Status == ToolCallStatus.AwaitingApproval)
                card.Decide(ToolApprovalDecision.Deny);
        }
    }

    private List<ChatMessage> BuildHistory(string latestUserMessage)
    {
        var history = new List<ChatMessage> { new("system", IsAgentMode ? AgentSystemPrompt : BaseSystemPrompt) };

        if (IsAgentMode)
        {
            history.Add(new ChatMessage(
                "system",
                _reportPath.Length > 0
                    ? $"The report currently open is: {_reportPath}"
                    : "No report is open. Ask the user for a file path before calling any tool."));
        }

        string context = LoadReportContext();
        if (context.Length > 0)
            history.Add(new ChatMessage(
                "system",
                $"Internal context for {_reportPath} — use it silently; never paste this block into chat:\n{context}"));

        foreach (var message in Messages
                     .OfType<ChatMessageViewModel>()
                     .Where(m => m.Content is not "Thinking..." &&
                                 !m.Content.StartsWith("Error:") &&
                                 m.Content != DefaultWelcome &&
                                 m.Content != AgentWelcome)
                     .TakeLast(MaxHistoryMessages))
        {
            string content = Truncate(message.Content, MaxHistoryMessageChars);
            history.Add(new ChatMessage(message.IsUser ? "user" : "assistant", content));
        }

        if (history.Count == 0 || history[^1].Content != latestUserMessage)
            history.Add(new ChatMessage("user", latestUserMessage));

        return history;
    }

    private string LoadReportContext()
    {
        if (_reportPath.Length == 0 || !File.Exists(_reportPath))
            return string.Empty;

        if (!IsRdlDefinition(_reportPath))
            return string.Empty;

        if (_cachedSummary is not null &&
            string.Equals(_cachedSummaryPath, _reportPath, StringComparison.OrdinalIgnoreCase))
            return _cachedSummary;

        _cachedSummary = RdlcContextSummarizer.Summarize(_reportPath);
        _cachedSummaryPath = _reportPath;
        return _cachedSummary;
    }

    private static bool IsRdlDefinition(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".rdlc", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".rdl", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;
        return text[..maxChars] + "…";
    }

    private void LoadHistoryForCurrentFile()
    {
        Messages.Clear();
        _toolCards.Clear();

        if (string.IsNullOrWhiteSpace(_reportPath))
        {
            ShowWelcome();
            return;
        }

        var stored = _historyStore.Load(_reportPath);
        if (stored.Count == 0)
        {
            ShowWelcome();
            return;
        }

        foreach (var msg in stored)
            Messages.Add(new ChatMessageViewModel { IsUser = msg.IsUser, Content = msg.Content });
    }

    private void PersistCurrentHistory()
    {
        if (string.IsNullOrWhiteSpace(_reportPath))
            return;

        // Tool cards are a record of one session's activity, not conversation, so they are not
        // carried across restarts.
        _historyStore.Save(
            _reportPath,
            Messages.OfType<ChatMessageViewModel>().Select(m => (m.IsUser, m.Content)));
    }

    private void ShowWelcome() =>
        Messages.Add(new ChatMessageViewModel { IsUser = false, Content = IsAgentMode ? AgentWelcome : DefaultWelcome });

    partial void OnModelChanged(string value)
    {
        if (_loadingSettings)
            return;

        PersistSelectedModel(value);
    }

    private void LoadSettingsIntoUi()
    {
        _loadingSettings = true;
        try
        {
            var s = _settingsService.Settings;
            ApiKey = s.ApiKey;
            BaseUrl = s.BaseUrl;
            AzureDeployment = s.AzureDeployment;
            SelectedProvider = s.Provider.ToString();
            ProviderDisplayName = CopilotProviderDefaults.DisplayName(s.Provider);
            RefreshModelOptions(s.Provider);
            Model = NormalizeModelId(s.Model);
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void RefreshModelOptions(CopilotProvider provider)
    {
        ModelOptions.Clear();
        foreach (var option in CopilotProviderDefaults.Models(provider))
            ModelOptions.Add(option);
    }

    private string NormalizeModelId(string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(modelId) &&
            ModelOptions.Any(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
            return modelId!;

        return CopilotProviderDefaults.DefaultModel(
            Enum.TryParse<CopilotProvider>(SelectedProvider, out var provider)
                ? provider
                : CopilotProvider.Groq);
    }

    private void PersistSelectedModel(string? modelId)
    {
        string normalized = NormalizeModelId(modelId);
        if (!string.Equals(Model, normalized, StringComparison.Ordinal))
        {
            _loadingSettings = true;
            try
            {
                Model = normalized;
            }
            finally
            {
                _loadingSettings = false;
            }
        }

        if (string.Equals(_settingsService.Settings.Model, normalized, StringComparison.Ordinal))
            return;

        _settingsService.Settings.Model = normalized;
        _settingsService.Save();
    }
}

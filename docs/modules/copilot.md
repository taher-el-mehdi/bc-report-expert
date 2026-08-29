# Copilot Module

**Project:** `src/ReportExpert.Modules.Copilot`  
**Entry view:** `Views/CopilotView.xaml`

## Purpose

AI assistant for RDLC reports — suggests improvements, finds errors, and (in agent mode) edits the report directly through MCP tools, with approval. Supports **Groq, Google Gemini, Mistral, OpenRouter, Claude (Anthropic), OpenAI, Azure OpenAI, and local Ollama** via `OpenAiCompatibleClient`.

## Folder structure

```
ReportExpert.Modules.Copilot/
├── Services/
│   ├── CopilotSettings.cs            Settings model + JSON persistence
│   ├── OpenAiCompatibleClient.cs     Unified client (Groq/OpenAI/Azure/Ollama), tool calling
│   ├── RexAgentLoop.cs               Model → tool calls → results → repeat, bounded
│   ├── IToolApprovalService.cs       Approval gate for tools that write
│   ├── AgentEvents.cs                Tool started/finished/denied, file changed
│   ├── CopilotToolExecutor.cs        Legacy validate_xml, run_preview, suggest_fix
│   ├── RdlcPatchParser.cs            Legacy rdlc-patch block parsing
│   └── RdlcXmlValidator.cs           XML validation for tool calls
├── Helpers/                          Converters + CopilotMessageTemplateSelector
├── ViewModels/
│   ├── CopilotViewModel.cs           Chat history, commands, context, approval
│   └── CopilotMessageViewModels.cs   Chat bubbles and tool cards
└── Views/
    └── CopilotView.xaml              Chat UI + settings panel
```

## Configuration

Settings file: `%AppData%\ReportExpert\copilot.json`

```json
{
  "Provider": "Gemini",
  "ApiKey": "",
  "Model": "gemini-2.5-flash",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai",
  "AzureDeployment": "",
  "AgentModeEnabled": true
}
```

Configure via the gear icon in the Copilot panel or the app **Settings** tab. Providers: Groq, Google Gemini, Mistral, OpenRouter, Claude (Anthropic), OpenAI, Azure OpenAI, Ollama. **Never commit API keys.**

API keys:
- Gemini: [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
- Mistral: [console.mistral.ai/api-keys](https://console.mistral.ai/api-keys)
- OpenRouter: [openrouter.ai/keys](https://openrouter.ai/keys)
- Claude: [console.anthropic.com/settings/keys](https://console.anthropic.com/settings/keys)
- Groq: [console.groq.com](https://console.groq.com)

## Important APIs

### `OpenAiCompatibleClient.CompleteAsync(messages)`

Sends a chat completion request to the configured provider. Throws if the API key is missing (when required) or the API returns an error.

### `CopilotViewModel.SetReportContext(string rdlcPath)`

Called by the shell when a report is opened in Preview. The file XML (truncated for large reports) is included on the next chat request.

### Quick-action commands

| Command | Prompt sent |
|---------|-------------|
| Suggest improvements | Review report and suggest concrete improvements |
| Find errors | Check for duplicate names, invalid expressions, schema issues |

## How context is built

History assembly typically includes:

1. Fixed system prompt (RDLC / Business Central expertise)
2. Current report XML (if loaded)
3. Recent chat messages
4. Latest user message

## Agent mode

Enable **Agent mode** in Settings. `RexAgentLoop` then offers the model every tool published by the
configured MCP servers — by default the twenty-nine RDLC tools of the bundled `rdlc-mcp` server —
and runs the calls it asks for, up to eight rounds per turn.

Each call appears as a card in the chat showing the tool, its arguments and its result. Read-only
tools run straight away; anything that writes waits for **Approve** or **Deny** on the card, and
**Approve for this turn** skips the remaining prompts in that turn. After a successful write,
`ShellCoordinator` reloads the file so the editor does not clobber it.

With agent mode off, or no MCP server reachable, the chat-only prompt and the legacy `rdlc-patch`
confirmation flow apply instead.

Details in [McpServer.md](../McpServer.md).

## Extending

- **New quick action:** add `[RelayCommand]` in `CopilotViewModel` calling the send-message pipeline
- **Different provider:** adjust BaseUrl / provider enum in settings (client already speaks OpenAI chat completions)
- **New tools:** add an MCP server to `%AppData%\ReportExpert\mcp-servers.json` — no module changes needed
- **Smarter context:** send parsed metadata instead of full XML for large reports

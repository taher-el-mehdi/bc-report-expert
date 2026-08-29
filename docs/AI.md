# AI assistant architecture

Report Expert’s **Copilot** helps authors understand and improve RDLC XML using OpenAI-compatible chat APIs.

## Components

| Piece | Location | Role |
|-------|----------|------|
| `CopilotView` / `CopilotViewModel` | `Modules.Copilot` | Chat UI, history, quick actions, agent mode, tool approval |
| `OpenAiCompatibleClient` | `Modules.Copilot/Services` | HTTP chat completions with tool calling (Groq / OpenAI / Azure / Ollama) |
| `RexAgentLoop` | `Modules.Copilot/Services` | Runs a turn: ask the model, run its tools, feed results back, repeat |
| `IToolApprovalService` | `Modules.Copilot/Services` | Gate before anything writes; implemented by `CopilotViewModel` |
| `CopilotSettingsService` | `Modules.Copilot/Services` | Persist provider settings to `%AppData%\ReportExpert\copilot.json` |
| `RdlcContextSummarizer` | `Modules.Copilot/Services` | Compact report context for prompts |
| `CopilotChatHistoryStore` | `Modules.Copilot/Services` | Optional local chat history persistence |
| `McpToolRegistry` | `Mcp.Client` | Aggregates MCP servers behind `IMcpToolGateway`, prefixes tool names |
| `McpHost` | `App/Shell` | Owns the MCP child processes and their configuration |
| `ShellCoordinator` | `App/Shell` | Supplies current XML, opens preview, reloads after an agent write |

```
User message
    ↓
CopilotViewModel (system prompt + RDLC context + history)
    ↓
RexAgentLoop ──→ OpenAiCompatibleClient → provider API
    ↑                     ↓
    │              tool calls requested
    │                     ↓
    │        approval (writes only) → IMcpToolGateway → rdlc-mcp
    └──────────── tool results ──────────┘
    ↓
Reply, and ShellCoordinator reloads any file that changed
```

## Context assembly

Typical request payload:

1. System prompt (RDLC / Business Central expertise, safety rules for patches)
2. Summarized or truncated current report XML (when a layout is selected)
3. Recent chat turns
4. Latest user message / quick-action template

Large reports are truncated or summarized so requests stay within model limits — see `RdlcContextSummarizer`.

## Agent mode

Turn it on in Settings. Rex is then given the tools published by the configured MCP servers — by
default the twenty-nine RDLC tools from the bundled `rdlc-mcp` server — and can inspect and edit the
open report itself instead of describing what you should change.

A turn runs like this:

1. The conversation and the tool schemas go to the provider.
2. If the model asks for tools, each call is executed and its result appended as a `tool` message.
3. Repeat, up to **eight** rounds. A model still calling tools after that is asked once more, with no
   tools on offer, so the turn ends in prose rather than silence.
4. Any report the agent wrote is reloaded from disk by `ShellCoordinator`.

Read-only tools run without asking. Anything that writes goes through `IToolApprovalService`, which
surfaces as approve/deny buttons on the tool card in the chat. Approving *for the turn* skips the
remaining prompts in that turn only. Every card shows the tool, its arguments and its result, so
nothing happens off-screen.

Tools execute in a local child process; the model receives tool results, never your file system or
API keys.

Without agent mode, or with no MCP server reachable, Rex stays in **chat-only** mode (explain and
suggest; it does not write files). `RdlcPatchParser` / `CopilotToolExecutor` remain in the Copilot
project as a legacy find/replace contract with unit tests; `CopilotViewModel` does not call them
today. See [CLEANUP_REVIEW.md](CLEANUP_REVIEW.md).

See [McpServer.md](McpServer.md) for the tools, the safety model and the response envelope.

## Providers

Configured via Settings or Copilot gear:

| Provider | Notes |
|----------|-------|
| Groq | Fast free-tier friendly |
| Google Gemini | OpenAI-compatible; strong tool calling |
| Mistral | OpenAI-compatible (`api.mistral.ai`) |
| OpenRouter | One key for many models (Claude, Gemini, GPT, …) |
| Claude (Anthropic) | Anthropic’s OpenAI-compat endpoint |
| OpenAI | Standard OpenAI API |
| Azure OpenAI | Requires deployment name + Azure base URL |
| Ollama | Local; API key often unused |

Defaults live in `CopilotProvider` / `CopilotProviderDefaults`.

## Security notes

- API keys are stored **plaintext** in AppData today — treat the machine as trusted; DPAPI encryption is on the roadmap
- Never commit `copilot.json`
- No tool writes to disk without confirmation, and every write is backed up first
- MCP servers are launched from `mcp-servers.json`; only add commands you trust, as they run with your privileges
- Do not paste secrets into chat history that may be persisted locally

## Extending

1. Add provider defaults in `CopilotProvider` enums/helpers
2. Keep `OpenAiCompatibleClient` as the single HTTP boundary
3. Add tools by adding an MCP server to `mcp-servers.json` — no app code changes; see [McpServer.md](McpServer.md)
4. Document behavior in [modules/copilot.md](modules/copilot.md)

## Related

- [McpServer.md](McpServer.md) — the RDLC MCP server and its tools
- [modules/copilot.md](modules/copilot.md)
- [Architecture.md](Architecture.md)
- [Roadmap.md](Roadmap.md)

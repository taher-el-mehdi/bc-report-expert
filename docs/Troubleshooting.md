# Troubleshooting

## Build fails: SDK / TFM not found

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) and confirm:

```bash
dotnet --list-sdks
```

Open `ReportExpert.slnx` with a recent Visual Studio 2022 or use the CLI.

## App starts but theme / logos look wrong

Theme follows Settings (`System` / `Light` / `Dark`). Logo assets are WPF resources under `ReportExpert.App/Assets`. Rebuild after asset changes.

## Preview PDF is blank or fails

1. Confirm the `.rdlc` opens and metadata parses (datasets visible on Home)
2. For BC XML mode, use an exported DataItems XML that matches dataset names
3. Check the status message in the preview window
4. Ensure ReportViewerCore packages restored (`Directory.Packages.props`)

## WebView2 does not show the PDF

Install the [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/). The app may still write the PDF and offer download / system open.

## Copilot returns unauthorized / 401

- Verify API key in Settings
- Confirm provider base URL and model name
- For Azure, set deployment name and the Azure-compatible base URL

## Copilot settings were reset

Corrupt `copilot.json` is replaced with defaults on load. Delete the file to reset intentionally.

## Excel / Word host issues

- Excel editing requires ReoGrid package restore
- Word preview uses Open XML → FlowDocument; complex DOCX may not render fully — use **Open in Word**

## Tests fail only locally

```bash
dotnet clean ReportExpert.slnx
dotnet test ReportExpert.slnx
```

Ensure no file locks on test fixtures under `tests/`.

## Still stuck?

Open a Bug Report with OS, SDK version, steps, and a minimal sample file (sanitized). See also [FAQ.md](FAQ.md).

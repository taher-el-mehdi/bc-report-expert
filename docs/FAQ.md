# FAQ

## What is Report Expert?

A Windows desktop tool for developing and previewing RDLC reports (especially Business Central ERP), with AI assistance.

## What is the current version?

**v1.0 Marmoset.** Releases use a numeric label plus an animal codename. The installer and portable ZIP are named `ReportExpert-Setup-v1.0-Marmoset.exe` and `ReportExpert-v1.0-Marmoset-win-x64.zip`. Settings shows the same label.

## Does it replace Visual Studio Report Designer?

No. It complements VS with fast local preview, sample data, PDF export, and Copilot help. Report Expert also includes a **native layout editor** (toolbox, datasets/fields, parameters, expression editor, textboxes/rectangles/lines/images, properties, undo/save). Charts/gauges/maps and full tablix structure design remain deferred — see [rdlc-designer/Architecture.md](rdlc-designer/Architecture.md).

## Which .NET version do I need?

**.NET 10 SDK**. The app targets `net10.0-windows`.

## Where are settings stored?

`%AppData%\ReportExpert\` — `settings.json`, `copilot.json`, recent files/projects. Do not commit these files.

## Why is Localization in the solution but missing from the UI?

The module exists but is not registered in shell navigation yet. See [Roadmap.md](Roadmap.md).

## Can I use Copilot offline?

Yes — point the provider to **Ollama** (or another OpenAI-compatible local endpoint). Cloud providers need an API key.

## Is my API key uploaded anywhere except the chosen provider?

No. The key is stored locally and sent only to the configured provider base URL.

## Why are there `Reporting.*` projects and also parsers inside Preview?

Preview owns the **live** UI path. `Reporting.*` are libraries for gradual consolidation and testing. See [Architecture.md](Architecture.md).

## License?

MIT for Report Expert source — see [LICENSE](../LICENSE). Microsoft Business Central sample reports are not included in this repository.

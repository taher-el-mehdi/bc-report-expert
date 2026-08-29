# Roadmap

Living plan for Report Expert. Items are goals, not commitments.

## Near term

- [ ] Wire **Localization** module into shell navigation (or explicitly defer and mark experimental)
- [ ] Adopt `Microsoft.Extensions.DependencyInjection` in App for service lifetimes
- [ ] Encrypt Copilot API keys at rest (DPAPI) instead of plaintext `copilot.json`
- [ ] Expand unit tests for `BcXmlReportRenderer` and Copilot patch parser
- [ ] Add UI screenshots to README
- [x] Native RDLC layout designer (`ReportExpert.RdlcDesigner`) — toolbox, data/params panes, expression editor, lines/images
- [ ] Designer next: tablix structure editing, charts/gauges, snap-to-grid / rulers

## Medium term

- [ ] Consolidate Preview live services onto `ReportExpert.Reporting.*` libraries
- [ ] Remove or productize Infrastructure / Application / Plugins scaffolding
- [ ] Shared converter library (`ReportExpert.Shared`) used by modules
- [ ] Optional headless PDF CLI for CI smoke tests
- [ ] Improve WebView2 failure UX and offline PDF fallback

## Longer term

- [ ] Plugin model for custom dummy-data providers and export formats
- [ ] Multi-report batch preview / export
- [ ] Deeper AL ↔ RDLC cross-navigation
- [ ] Non-Windows exploration only if ReportViewer alternatives mature

Propose changes via Feature Request issues or Discussions **Ideas**.

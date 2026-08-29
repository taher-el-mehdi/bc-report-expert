# Security policy

## Supported versions

Report Expert **v1.0 Marmoset** is the current release. Security fixes target the default branch.

## Reporting a vulnerability

Please **do not** open a public issue for sensitive security reports.

Email **mehditaher01@outlook.com** with:

- Description of the issue
- Steps to reproduce
- Impact assessment (if known)
- Whether you plan a coordinated disclosure timeline

We will acknowledge receipt and work on a fix as capacity allows.

## Known considerations

- Copilot API keys are stored in plaintext under `%AppData%\ReportExpert\copilot.json`. Treat the local machine as trusted; DPAPI encryption is on the roadmap.
- Never commit API keys, `.env` files, or customer report data.

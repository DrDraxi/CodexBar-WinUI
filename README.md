# CodexBar - Windows Fork

This is a Windows-focused fork of the original macOS CodexBar.

Tiny Windows tray app that keeps your Codex, Claude, Cursor, Gemini, Antigravity, Droid (Factory), Copilot, z.ai, Kiro, Vertex AI, Augment, Amp, and JetBrains AI limits visible (session + weekly where available) and shows when each window resets. One status item per provider (or Merge Icons mode); enable what you use from Settings.

<img src="codexbar.png" alt="CodexBar menu screenshot" width="520" />

## Install

### Requirements
- Windows 10/11

### GitHub Releases
Download: <https://github.com/Finesssee/Win-CodexBar/releases>

## Providers

- [Codex](docs/codex.md) â€” Local Codex CLI RPC (+ PTY fallback) and optional OpenAI web dashboard extras.
- [Claude](docs/claude.md) â€” OAuth API or browser cookies (+ CLI PTY fallback); session + weekly usage.
- [Cursor](docs/cursor.md) â€” Browser session cookies for plan + usage + billing resets.
- [Gemini](docs/gemini.md) â€” OAuth-backed quota API using Gemini CLI credentials (no browser cookies).
- [Antigravity](docs/antigravity.md) â€” Local language server probe (experimental); no external auth.
- [Droid](docs/factory.md) â€” Browser cookies + WorkOS token flows for Factory usage + billing.
- [Copilot](docs/copilot.md) â€” GitHub device flow + Copilot internal usage API.
- [z.ai](docs/zai.md) â€” API token for quota + MCP windows.
- [Kimi](docs/kimi.md) â€” Auth token (JWT from `kimi-auth` cookie) for weekly quota + 5â€‘hour rate limit.
- [Kimi K2](docs/kimi-k2.md) â€” API key for credit-based usage totals.
- [Kiro](docs/kiro.md) â€” CLI-based usage via `kiro-cli /usage` command; monthly credits + bonus credits.
- [Vertex AI](docs/vertexai.md) â€” Google Cloud gcloud OAuth with token cost tracking from local Claude logs.
- [Augment](docs/augment.md) â€” Browser cookie-based authentication with automatic session keepalive; credits tracking and usage monitoring.
- [Amp](docs/amp.md) â€” Browser cookie-based authentication with Amp Free usage tracking.
- [JetBrains AI](docs/jetbrains.md) â€” Local XML-based quota from JetBrains IDE configuration; monthly credits tracking.
- Open to new providers: [provider authoring guide](docs/provider.md).

## Icon & Screenshot
The tray icon is a tiny two-bar meter:
- Top bar: 5â€‘hour/session window. If weekly is missing/exhausted and credits are available, it becomes a thicker credits bar.
- Bottom bar: weekly window (hairline).
- Errors/stale data dim the icon; status overlays indicate incidents.

## Features
- Multi-provider tray with per-provider toggles (Settings â†’ Providers).
- Session + weekly meters with reset countdowns.
- Optional Codex web dashboard enrichments (code review remaining, usage breakdown, credits history).
- Local cost-usage scan for Codex + Claude (last 30 days).
- Provider status polling with incident badges in the menu and icon overlay.
- Merge Icons mode to combine providers into one status item + switcher.
- Refresh cadence presets (manual, 1m, 2m, 5m, 15m).
- Bundled CLI (`codexbar`) for scripts and CI (including `codexbar cost --provider codex|claude` for local cost usage); Linux CLI builds available.
- Privacy-first: on-device parsing by default; browser cookies are opt-in and reused (no passwords stored).

## Privacy note
Wondering if CodexBar scans your disk? It doesnâ€™t crawl your filesystem; it reads a small set of known locations (browser cookies/local storage, local JSONL logs) when the related features are enabled. See the discussion and audit notes in [issue #12](https://github.com/steipete/CodexBar/issues/12).

## Windows permissions (why they're needed)
- **Files & folders access**: CodexBar launches provider CLIs (codex/claude/gemini/antigravity). If those CLIs read a project directory or external drive, Windows may prompt for access. This is driven by the CLI's working directory, not background disk scanning.
- **Credential storage**: provider tokens/cookies are stored using Windows APIs (no passwords are stored).

## Docs
- Providers overview: [docs/providers.md](docs/providers.md)
- Provider authoring: [docs/provider.md](docs/provider.md)
- UI & icon notes: [docs/ui.md](docs/ui.md)
- CLI reference: [docs/cli.md](docs/cli.md)
- Architecture: [docs/architecture.md](docs/architecture.md)
- Refresh loop: [docs/refresh-loop.md](docs/refresh-loop.md)
- Status polling: [docs/status.md](docs/status.md)
- Release checklist: [docs/RELEASING.md](docs/RELEASING.md)

## Getting started (dev)
- Clone the repo and open the solution in Visual Studio.
- Launch once, then toggle providers in Settings â†’ Providers.
- Install/sign in to provider sources you rely on (CLIs, browser cookies, or OAuth).
- Optional: set OpenAI cookies (Automatic or Manual) for Codex dashboard extras.

## Build from source
Open src/CodexBar/CodexBar.sln in Visual Studio and build the CodexBar project.

## Related
- âś‚ď¸Ź [Trimmy](https://github.com/steipete/Trimmy) â€” â€śPaste once, run once.â€ť Flatten multi-line shell snippets so they paste and run.
- đź§ł [MCPorter](https://mcporter.dev) â€” TypeScript toolkit + CLI for Model Context Protocol servers.
- đź§ż [oracle](https://askoracle.dev) â€” Ask the oracle when you're stuck. Invoke GPT-5 Pro with a custom context and files.

## Looking for a Windows version?
You are here. This repository is the Windows fork.

## Credits
Inspired by [ccusage](https://github.com/ryoppippi/ccusage) (MIT), specifically the cost usage tracking.

## License
MIT â€˘ Peter Steinberger ([steipete](https://twitter.com/steipete))


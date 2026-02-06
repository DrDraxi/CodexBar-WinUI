# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CodexBar is a Windows system tray application that tracks usage limits for AI coding assistants. It's a WinUI 3 port of the original macOS CodexBar. Supports 17 providers: Claude, Codex, Copilot, Cursor, Gemini, JetBrains, Augment, Kiro, Amp, Factory, Zai, Kimi, Kimi K2, MiniMax, Vertex AI, OpenCode, and Antigravity.

## Build Commands

```bash
# Build the solution
dotnet build

# Build release
dotnet build --configuration Release

# Run the app (x64)
dotnet run --project src/CodexBar/CodexBar.csproj -p:Platform=x64

# Publish single-file exe (x64)
dotnet publish src/CodexBar/CodexBar.csproj --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:WindowsPackageType=None -o publish
```

## Architecture

### Solution Structure

- **CodexBar** (`src/CodexBar/`) - WinUI 3 app with system tray, popup window, and settings UI
- **CodexBar.Core** (`src/CodexBar.Core/`) - Shared library with provider fetchers, models, and auth logic
- **TaskbarWidget** (`lib/taskbar-widget/`) - Git submodule for taskbar widget injection

After cloning, initialize the submodule:
```bash
git submodule update --init --recursive
```

### Provider System

Each AI service is a "provider" implementing `IProviderFetcher`:
- `IsAvailableAsync()` - Check if credentials exist (browser cookies, OAuth tokens, etc.)
- `FetchAsync()` - Return a `UsageSnapshot` with usage percentages

Providers are registered in `ProviderRegistry.cs`. Each provider folder (e.g., `Providers/Cursor/`) contains:
- API models (JSON deserialization)
- Fetcher implementation

### Key Models

- `UsageSnapshot` - Contains Primary/Secondary/Tertiary/Quaternary `RateWindow` plus optional `ProviderCost`
- `RateWindow` - Usage percentage, reset time, optional custom label
- `ProviderCostSnapshot` - On-demand/pay-per-use tracking (e.g., Cursor on-demand spend, Claude extra usage)
- `BarVisibilitySettings` - Per-provider toggles for which bars show in popup vs widget

### Authentication

Providers use different auth methods:
- **Browser cookies**: Extracted from Chrome/Edge SQLite databases (`Auth/ChromeCookieExtractor.cs`) — Cursor, Amp, Factory, Kimi, OpenCode, Augment
- **OAuth tokens**: Read from CLI config files (e.g., `~/.claude/.credentials.json`, `~/.codex/auth.json`) — Claude, Codex, Gemini
- **API tokens**: From environment variables or config files — Zai (`ZAI_API_TOKEN`), KimiK2 (`KIMI_K2_API_KEY`), MiniMax (`MINIMAX_API_TOKEN`)
- **CLI credentials**: Local CLI tools — Kiro (`~/.kiro/usage.json` or `kiro usage --json`), VertexAI (`gcloud` ADC)
- **Local detection**: Process/port probing — Antigravity, JetBrains
- **Manual cookies/tokens**: User can paste cookies or tokens in settings, stored via `ManualCookieStore`

### App Lifecycle

`App.xaml.cs` manages:
- Single-instance mutex
- System tray icon (H.NotifyIcon)
- Periodic refresh timer
- Cached snapshots for instant popup display

### Debug Logging

Logs written to `%LOCALAPPDATA%/CodexBar/debug.log` via `DebugLog.Log()`. Check this file when debugging provider API responses.

## Gotchas

- **Platform required**: WinUI 3 requires explicit platform. Use `-p:Platform=x64` for run commands or you'll get architecture errors.

## Releases

Version is derived from git tags. The GitHub Actions workflow automatically creates releases when a tag is pushed.

### How to Release

1. **Update CHANGELOG.md** with the new version section:
   ```markdown
   ## [v1.2.0] - YYYY-MM-DD

   ### Added
   - New feature description

   ### Changed
   - Changed behavior description

   ### Fixed
   - Bug fix description
   ```

2. **Commit the changelog**:
   ```bash
   git add CHANGELOG.md
   git commit -m "docs: update changelog for v1.2.0"
   git push
   ```

3. **Create and push the tag**:
   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```

4. The workflow will automatically:
   - Build the exe and zip artifacts
   - Extract release notes from CHANGELOG.md for this version
   - Create a GitHub release with the artifacts and notes

### Changelog Format

Follow [Keep a Changelog](https://keepachangelog.com/) format:
- `### Added` - New features
- `### Changed` - Changes in existing functionality
- `### Deprecated` - Soon-to-be removed features
- `### Removed` - Removed features
- `### Fixed` - Bug fixes
- `### Security` - Security fixes

### Version Numbering

Follow [Semantic Versioning](https://semver.org/):
- **Major** (v2.0.0): Breaking changes
- **Minor** (v1.1.0): New features, backwards compatible
- **Patch** (v1.0.1): Bug fixes, backwards compatible

## Commit Guidelines

Do not add `Co-Authored-By: Claude` or similar co-author lines to commits.

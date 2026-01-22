# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CodexBar is a Windows system tray application that tracks usage limits for AI coding assistants (Cursor, Claude, Codex CLI, etc.). It's a WinUI 3 port of the original macOS CodexBar.

## Build Commands

```bash
# Build the solution
dotnet build

# Build release
dotnet build --configuration Release

# Publish single-file exe (x64)
dotnet publish src/CodexBar/CodexBar.csproj --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:WindowsPackageType=None -o publish
```

## Architecture

### Solution Structure

- **CodexBar** (`src/CodexBar/`) - WinUI 3 app with system tray, popup window, and settings UI
- **CodexBar.Core** (`src/CodexBar.Core/`) - Shared library with provider fetchers, models, and auth logic

### Provider System

Each AI service is a "provider" implementing `IProviderFetcher`:
- `IsAvailableAsync()` - Check if credentials exist (browser cookies, OAuth tokens, etc.)
- `FetchAsync()` - Return a `UsageSnapshot` with usage percentages

Providers are registered in `ProviderRegistry.cs`. Each provider folder (e.g., `Providers/Cursor/`) contains:
- API models (JSON deserialization)
- Fetcher implementation

### Key Models

- `UsageSnapshot` - Contains Primary/Secondary/Tertiary `RateWindow` plus optional `ProviderCost`
- `RateWindow` - Usage percentage, reset time, optional custom label
- `ProviderCostSnapshot` - On-demand/pay-per-use tracking (e.g., Cursor on-demand spend)

### Authentication

Providers use different auth methods:
- **Browser cookies**: Extracted from Chrome/Edge SQLite databases (`Auth/ChromeCookieExtractor.cs`)
- **OAuth tokens**: Read from CLI config files (e.g., `~/.claude/.credentials.json`, `~/.codex/auth.json`)
- **Manual cookies**: User can paste cookies in settings, stored via `ManualCookieStore`

### App Lifecycle

`App.xaml.cs` manages:
- Single-instance mutex
- System tray icon (H.NotifyIcon)
- Periodic refresh timer
- Cached snapshots for instant popup display

### Debug Logging

Logs written to `%LOCALAPPDATA%/CodexBar/debug.log` via `DebugLog.Log()`. Check this file when debugging provider API responses.

## Releases

Version is derived from git tags. To release:
1. `git tag v1.x.x && git push origin v1.x.x`
2. Create GitHub release - workflow builds and uploads exe/zip artifacts

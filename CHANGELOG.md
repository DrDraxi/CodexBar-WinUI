# Changelog

All notable changes to CodexBar will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v2.0.2] - 2026-02-13

### Fixed
- Widgets no longer display over fullscreen applications

## [v2.0.1] - 2026-02-12

### Changed
- Smooth lerp animation when dragging widgets to reorder
- Smooth animation when neighboring widget resizes (widgets slide instead of snapping)

## [v2.0.0] - 2026-02-12

### Changed
- Widget rendering migrated from WinUI 3 XAML to pure Win32 GDI via `TaskbarWidget.Widget` API
- Widget code reduced from ~380 lines XAML + ~185 lines wrapper to ~160 lines
- No longer requires `DesktopWindowXamlSource` for taskbar widget

### Added
- Drag-to-reorder support — reorder widgets by dragging
- Cross-widget atomic repositioning when widget resizes

### Removed
- `TaskbarWidgetContent.xaml` / `.xaml.cs` (replaced by GDI render callback)
- `TaskbarWidget.cs` wrapper (replaced by `CodexBarWidget.cs`)
- `TaskbarStructureWatcher` and `Interop/Native.cs` (Widget API handles positioning)

## [v1.6.0] - 2025-02-06

### Added
- 10 new AI providers: Kiro, Amp, Factory, Zai, Kimi, Kimi K2, MiniMax, Vertex AI, OpenCode, Antigravity
- Quaternary rate window support (up to 4 usage bars per provider + cost)
- Per-provider bar visibility settings for both popup and taskbar widget
- Claude: Sonnet 7d and Opus 7d model-specific usage bars (for Max tier)
- Bar visibility UI in provider settings with human-readable bar names

### Changed
- Enabled previously disabled providers: Copilot, Gemini, JetBrains, Augment
- Widget defaults to showing only Primary + Secondary + Cost bars (model-specific bars hidden to save space)
- Claude bars now labeled: 5h, 7d, Sonnet 7d, Opus 7d, Extra Usage

### Fixed
- Popup and widget now render Tertiary and Quaternary bars (previously skipped)

## [v1.5.1] - 2025-02-06

### Fixed
- Claude provider: automatically refresh expired OAuth tokens using the refresh token
- Claude provider: retry on 401 responses with a fresh token before failing
- Claude provider: expired tokens with a valid refresh token now correctly report as available

## [v1.5.0] - 2025-02-05

### Added
- Dynamic widget resizing when provider count changes

### Changed
- Improved hover effect to look more native

## [v1.4.0] - 2025-02-03

### Changed
- Refactored taskbar widget injection to use shared submodule

## [v1.3.0] - 2025-01-23

### Changed
- Taskbar widget now enabled by default for new installations

## [v1.2.0] - 2025-01-23

### Added
- Hover effect on widget provider sections for visual feedback

### Changed
- Widget now repositions within 500ms when tray icons change (previously waited for minute refresh)

## [v1.1.0] - 2025-01-22

### Added
- **Taskbar Widget**: Compact usage bars displayed directly in the Windows taskbar
  - Shows vertical bars for each provider (primary, secondary, and cost limits)
  - Hover for tooltips with percentages and reset times
  - Click widget to open the full usage popup
  - Automatically repositions when taskbar icons change
- Settings toggle to show/hide the taskbar widget
- Usage data now refreshes when opening the popup

![Widget Tooltip](https://github.com/DrDraxi/CodexBar-WinUI/raw/main/docs/widget-tooltip.png)

## [v1.0.3] - 2025-01-21

### Fixed
- Minor bug fixes and stability improvements

## [v1.0.2] - 2025-01-20

### Fixed
- Cookie extraction improvements

## [v1.0.1] - 2025-01-19

### Fixed
- Initial bug fixes after release

## [v1.0.0] - 2025-01-18

### Added
- Initial release of CodexBar for Windows
- System tray application with usage popup
- Support for multiple AI providers:
  - Claude (OAuth and browser cookies)
  - Cursor (browser cookies)
  - Codex CLI (OAuth)
  - GitHub Copilot
  - Gemini
  - JetBrains AI
  - Augment
- Automatic provider detection on first launch
- Configurable refresh interval
- Settings UI for managing providers and cookies
- Start with Windows option

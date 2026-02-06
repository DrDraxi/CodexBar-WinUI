# Changelog

All notable changes to CodexBar will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

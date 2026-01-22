# CodexBar Windows - Feature TODO

Comparison with original macOS Swift CodexBar. Features marked with checkboxes.

## Providers

### Implemented & Tested
- [x] Claude (OAuth + Web cookies)
- [x] Codex (OAuth)
- [x] Cursor (browser cookies)

### Implemented but Untested
- [ ] Copilot (GitHub device flow) - skeleton exists
- [ ] Gemini (gcloud OAuth) - skeleton exists
- [ ] JetBrains (local XML) - skeleton exists
- [ ] Augment (browser cookies) - skeleton exists

### Not Implemented
- [ ] Kimi (JWT auth from cookie)
- [ ] Kimi K2 (API key)
- [ ] z.ai (API token)
- [ ] MiniMax (cookies/API)
- [ ] Amp (browser cookies)
- [ ] Factory/Droid (WorkOS)
- [ ] OpenCode (browser cookies)
- [ ] Antigravity (local probe)
- [ ] VertexAI (gcloud OAuth)
- [ ] Kiro (CLI)
- [ ] Synthetic (API key)

## UI Features

### Implemented
- [x] System tray icon
- [x] Usage popup with provider cards
- [x] Progress bars for usage windows
- [x] Reset time countdown display
- [x] On-demand/cost tracking display
- [x] Settings window

### Not Implemented
- [ ] Custom tray icon showing usage fill level
- [ ] Merge icons mode (combine all providers into one icon)
- [ ] Status overlay indicators on tray icon
- [ ] "Show usage as used" vs "remaining" toggle
- [ ] Absolute clock reset time option (vs countdown)
- [ ] Provider ordering in UI

## Settings

### Implemented
- [x] Refresh interval
- [x] Start with Windows
- [x] Enable/disable providers
- [x] Manual cookie entry per provider

### Not Implemented
- [ ] Cookie source selection (auto/manual/off per provider)
- [ ] Show usage as used vs remaining
- [ ] Check provider status toggle
- [ ] Provider ordering/reordering
- [ ] Debug logging toggle in UI

## Advanced Features

### Not Implemented
- [ ] Status page monitoring (Statuspage.io integration)
- [ ] Multi-account token support
- [ ] Account switcher UI
- [ ] Local cost tracking (scan Claude/Codex logs for token usage)
- [ ] CLI tool (`codexbar` command)
- [ ] Config file validation
- [ ] Environment variable support for API keys

## Priority Recommendations

### High Priority
1. Test and fix Copilot provider (popular tool)
2. Status page monitoring (useful for knowing if service is down)
3. Custom tray icon with usage fill level

### Medium Priority
4. Multi-account support
5. Test Gemini/JetBrains/Augment providers
6. Show usage as used vs remaining toggle

### Low Priority
7. Additional providers (Kimi, z.ai, MiniMax, etc.)
8. CLI tool
9. Local cost tracking

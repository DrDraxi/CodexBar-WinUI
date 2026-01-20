# CodexBar Swift Codebase Reference

This document captures the technical details from the original macOS Swift codebase for reference during Windows development.

> **Note:** The per-provider documentation files (e.g., `claude.md`, `codex.md`, `cursor.md`) contain additional API details and are still valid references. The "Key files" sections in those docs reference Swift source files that have been archived to `swift/` or removed.

## See Also

- Per-provider docs: `docs/{provider}.md` - Detailed API endpoints and authentication flows
- Windows implementation: `src/CodexBar.Core/Providers/` - C# provider implementations

## Overview

CodexBar is a menu bar application that tracks usage across multiple AI providers. The application maintains detailed usage metrics, cost tracking, and supports multiple authentication methods.

## All Supported Providers

| Provider | ID | Display Name | Primary Auth | Status Page | Dashboard |
|----------|----|--------------|--------------|-----------|-----------
| Claude | `claude` | Claude | OAuth/Web/CLI | https://status.claude.com | https://console.anthropic.com/settings/billing |
| Codex | `codex` | Codex | OAuth/Web/CLI | https://status.openai.com | https://chatgpt.com/codex/settings/usage |
| Copilot | `copilot` | Copilot | API Token | https://www.githubstatus.com | https://github.com/settings/copilot |
| Cursor | `cursor` | Cursor | Browser Cookies | https://status.cursor.com | https://cursor.com/dashboard?tab=usage |
| OpenCode | `opencode` | OpenCode | Browser Cookies | - | https://opencode.ai |
| Factory | `factory` | Factory (Droid) | Browser Cookies | https://status.factory.ai | https://app.factory.ai/settings/billing |
| Gemini | `gemini` | Gemini | OAuth (gcloud) | - | https://gemini.google.com |
| Antigravity | `antigravity` | Antigravity | Local Probe | - | - |
| Augment | `augment` | Augment | Browser Cookies | - | https://app.augmentcode.com/account/subscription |
| JetBrains | `jetbrains` | JetBrains AI | IDE XML | - | - |
| Kimi | `kimi` | Kimi | Auth Token (JWT) | - | https://www.kimi.com/code/console |
| KimiK2 | `kimik2` | Kimi K2 | API Key | - | https://kimi-k2.ai/my-credits |
| Kiro | `kiro` | Kiro | CLI | - | https://app.kiro.dev/account/usage |
| VertexAI | `vertexai` | Vertex AI | OAuth (gcloud) | - | https://console.cloud.google.com/vertex-ai |
| z.ai | `zai` | z.ai | API Token | - | https://z.ai/manage-apikey/subscription |
| MiniMax | `minimax` | MiniMax | Cookies/API Token | - | https://platform.minimax.io/user-center/payment/coding-plan |
| Amp | `amp` | Amp | Browser Cookies | - | https://ampcode.com/settings |
| Synthetic | `synthetic` | Synthetic | API Key | - | - |

---

## Provider Implementation Details

### 1. Claude

**Authentication Methods:**
- **OAuth** (preferred): Requires `user:profile` scope
- **Web API**: Browser cookies with `sessionKey`
- **CLI**: Direct `claude` command invocation
- **Manual Cookie**: Direct cookie header provision

**Credential Locations:**
- OAuth: `~/.claude/.credentials.json`
- Cookie: Browser extraction (Chrome, Edge, Firefox)

**API Endpoints:**

```
# OAuth Usage (preferred)
GET https://api.anthropic.com/api/oauth/usage
Headers:
  Authorization: Bearer {access_token}
  anthropic-beta: oauth-2025-04-20

# Web API - Get Organizations
GET https://claude.ai/api/organizations
Headers:
  Cookie: sessionKey={cookie_value}

# Web API - Usage Data
GET https://claude.ai/api/organizations/{org_uuid}/usage
Headers:
  Cookie: sessionKey={cookie_value}

# Web API - Extra Usage Limits
GET https://claude.ai/api/organizations/{org_uuid}/overage_spend_limit
Headers:
  Cookie: sessionKey={cookie_value}

# Web API - Account Info
GET https://claude.ai/api/account
Headers:
  Cookie: sessionKey={cookie_value}
```

**OAuth Response Structure:**
```json
{
  "five_hour": {
    "utilization": 45.5,
    "resets_at": "2025-01-21T18:00:00Z"
  },
  "seven_day": {
    "utilization": 62.3,
    "resets_at": "2025-01-28T00:00:00Z"
  },
  "seven_day_opus": {
    "utilization": 30.0,
    "resets_at": "2025-01-28T00:00:00Z"
  },
  "extra_usage": {
    "is_enabled": true,
    "monthly_limit": 5000,
    "used_credits": 1230,
    "currency": "USD"
  }
}
```

**Notes:**
- `utilization` field (not `percent_used`) contains 0-100 percentage
- Extra usage amounts are in **cents**, convert to dollars for display
- Cookie must start with `sk-ant-` prefix

---

### 2. Codex (OpenAI/ChatGPT)

**Authentication Methods:**
- **OAuth**: Tokens from `~/.codex/auth.json`
- **Web API**: Browser cookies from chatgpt.com
- **CLI**: Direct `codex` command

**Credential Locations:**
- OAuth: `~/.codex/auth.json`
- Cookie: Browser extraction from `chatgpt.com`

**OAuth Token Refresh:**
```
POST https://auth.openai.com/oauth/token
Content-Type: application/x-www-form-urlencoded

client_id=app_EMoamEEZ73f0CkXaXp7hrann
grant_type=refresh_token
refresh_token={refresh_token}
scope=openid profile email
```

**API Endpoints:**

```
# Usage Data
GET https://chatgpt.com/backend-api/wham/usage
Headers:
  Authorization: Bearer {access_token}
  User-Agent: CodexBar
  ChatGPT-Account-Id: {account_id}  # if available
```

**Response Structure:**
```json
{
  "plan_type": "plus",
  "rate_limit": {
    "primary_window": {
      "used_percent": 45.5,
      "limit_window_seconds": 10800,
      "reset_after_seconds": 3600
    },
    "secondary_window": {
      "used_percent": 62.0,
      "limit_window_seconds": 604800,
      "reset_after_seconds": 259200
    }
  },
  "credits": {
    "has_credits": true,
    "unlimited": false,
    "balance": 50.0
  }
}
```

**Notes:**
- Token refresh needed every 8+ days
- `rate_limit.primary_window` format (not `rate_limits` plural)
- Credits balance shows remaining credits

---

### 3. Cursor

**Authentication:**
- Browser cookies from `cursor.com`

**Cookie Names (in priority order):**
1. `WorkosCursorSessionToken`
2. `__Secure-next-auth.session-token`
3. `next-auth.session-token`

**API Endpoints:**

```
# Usage Summary
GET https://cursor.com/api/usage-summary
Headers:
  Cookie: {cookie_name}={cookie_value}

# User Info
GET https://cursor.com/api/auth/me
Headers:
  Cookie: {cookie_name}={cookie_value}
```

**Response Structure:**
```json
{
  "membershipType": "pro",
  "billingCycleEnd": "2025-02-01T00:00:00Z",
  "individualUsage": {
    "plan": {
      "used": 150,
      "limit": 500,
      "totalPercentUsed": 30
    },
    "onDemand": {
      "enabled": true,
      "used": 3500,
      "limit": 5000,
      "hardLimit": 10000
    }
  },
  "teamUsage": {
    "plan": {
      "used": 0,
      "limit": 0
    }
  }
}
```

**Notes:**
- On-demand usage is in **cents**, convert to dollars
- If on-demand is enabled and has usage, plan is at 100%
- `hardLimit` is the absolute maximum

---

### 4. Copilot (GitHub)

**Authentication:**
- GitHub OAuth token via device flow
- Environment: `COPILOT_API_TOKEN` or `GITHUB_TOKEN`

**Device Flow:**
```
# Step 1: Request device code
POST https://github.com/login/device/code
Content-Type: application/json

{
  "client_id": "{client_id}",
  "scope": "read:user"
}

Response:
{
  "device_code": "ABC123...",
  "user_code": "WXYZ-1234",
  "verification_uri": "https://github.com/login/device",
  "expires_in": 900,
  "interval": 5
}

# Step 2: Poll for token
POST https://github.com/login/oauth/access_token
(poll with device_code until user authorizes)
```

**API Endpoint:**

```
GET https://api.github.com/copilot_internal/user
Headers:
  Authorization: token {github_token}
  Accept: application/json
  Editor-Version: vscode/1.96.2
  Editor-Plugin-Version: copilot-chat/0.26.7
  User-Agent: GitHubCopilotChat/0.26.7
  X-Github-Api-Version: 2025-04-01
```

**Response Structure:**
```json
{
  "copilot_plan": "pro",
  "quota_snapshots": {
    "premium_interactions": {
      "percent_remaining": 85
    },
    "chat": {
      "percent_remaining": 60
    }
  }
}
```

**Notes:**
- `premium_interactions` → Primary (Session) window
- `chat` → Secondary (Weekly) window
- Values are percent **remaining**, not used

---

### 5. Gemini

**Authentication:**
- OAuth via gcloud CLI credentials
- Location: `~/.config/gcloud/application_default_credentials.json`

**Notes:**
- Uses Google Cloud ADC (Application Default Credentials)
- No direct usage API - relies on local credential detection

---

### 6. Kimi

**Authentication:**
- JWT token from `kimi-auth` cookie or `KIMI_AUTH_TOKEN` env var
- JWT contains: `device_id`, `ssid` (session ID), `sub` (traffic ID)

**API Endpoint:**

```
POST https://www.kimi.com/apiv2/kimi.gateway.billing.v1.BillingService/GetUsages
Headers:
  Authorization: Bearer {token}
  Cookie: kimi-auth={token}
  Content-Type: application/json
  Origin: https://www.kimi.com
  Referer: https://www.kimi.com/code/console
  connect-protocol-version: 1
  x-language: en-US
  x-msh-platform: web
  r-timezone: {timezone}
  x-msh-device-id: {from_jwt}
  x-msh-session-id: {from_jwt}
  x-traffic-id: {from_jwt}

Body:
{
  "scope": ["FEATURE_CODING"]
}
```

**Response Structure:**
```json
{
  "usages": [
    {
      "scope": "FEATURE_CODING",
      "detail": {
        "used": 150,
        "limit": 1000
      },
      "limits": [
        {
          "detail": {
            "used": 45,
            "limit": 200
          }
        }
      ]
    }
  ]
}
```

---

### 7. Kimi K2

**Authentication:**
- API Key from `KIMI_K2_API_KEY` env var

**API Endpoint:**
```
GET https://api.kimi-k2.ai/v1/credits
Headers:
  Authorization: Bearer {api_key}
```

---

### 8. z.ai

**Authentication:**
- API Token from `ZAI_API_TOKEN` env var

**API Endpoint:**
```
GET https://api.z.ai/v1/usage
Headers:
  Authorization: Bearer {api_token}
```

---

### 9. MiniMax

**Authentication Methods:**
- Browser cookies from `platform.minimax.io`
- API Token via `MINIMAX_API_TOKEN`

**Environment Variables:**
- `MINIMAX_COOKIE`: Raw cookie header
- `MINIMAX_AUTHORIZATION`: Bearer token
- `MINIMAX_GROUP_ID`: Organization group ID
- `MINIMAX_HOST_OVERRIDE`: Custom host URL

**API Endpoints:**

```
# HTML Dashboard (primary - parses __NEXT_DATA__)
GET https://platform.minimax.io/user-center/payment/coding-plan?cycle_type=3

# Remains API (fallback)
GET https://api.minimaxi.com/v1/api/openplatform/coding_plan/remains
Headers:
  Authorization: Bearer {token}
  Accept: application/json
  MM-API-Source: CodexBar
```

**Response Structure (Remains API):**
```json
{
  "base_resp": {
    "status_code": 0,
    "status_msg": "success"
  },
  "data": {
    "plan_name": "Premium",
    "model_remains": [
      {
        "current_interval_total_count": 1000,
        "current_interval_usage_count": 250,
        "start_time": 1702502400000,
        "end_time": 1705094400000,
        "remains_time": 2592000000
      }
    ]
  }
}
```

**Regions:**
- Global: `https://api.minimaxi.com`
- China: `https://api.minimax.cn`

---

### 10. Augment

**Authentication:**
- Browser cookies from `app.augmentcode.com`

**Notes:**
- Requires session keepalive
- Credits-based tracking

---

### 11. Amp

**Authentication:**
- Browser cookies from `ampcode.com`

**Notes:**
- HTML parsing for usage data
- Amp Free tier tracking

---

### 12. JetBrains AI

**Authentication:**
- Local XML configuration from JetBrains IDE

**Credential Location:**
- Windows: `%APPDATA%\JetBrains\{IDE}\options\ai.xml`
- macOS: `~/Library/Application Support/JetBrains/{IDE}/options/ai.xml`

**Notes:**
- Monthly credits tracking
- No API - reads local IDE configuration

---

### 13. Kiro

**Authentication:**
- CLI-based via `kiro-cli /usage` command

**Notes:**
- Monthly credits + bonus credits
- AWS-backed service

---

### 14. VertexAI

**Authentication:**
- Google Cloud OAuth via gcloud
- Location: `~/.config/gcloud/application_default_credentials.json`

**Token Refresh:**
```
POST https://oauth2.googleapis.com/token
(standard OAuth2 refresh flow)
```

**Notes:**
- Cost tracking from local Claude logs
- Uses Cloud Monitoring API for quota metrics

---

### 15. Factory (Droid)

**Authentication:**
- Browser cookies + WorkOS token flows

**Status Page:** https://status.factory.ai

---

## Data Models

### RateWindow
```
UsedPercent: double (0-100)
WindowMinutes: int? (e.g., 300 for 5h, 10080 for 7d)
ResetsAt: DateTime?
```

### UsageSnapshot
```
Primary: RateWindow (session/premium quota)
Secondary: RateWindow? (weekly/chat quota)
Tertiary: RateWindow? (model-specific like Opus)
ProviderCost: ProviderCostSnapshot?
Identity: ProviderIdentity?
UpdatedAt: DateTime
```

### ProviderCostSnapshot
```
Used: double (in currency units)
Limit: double (monthly limit)
CurrencyCode: string ("USD", "Credits", etc.)
Period: string ("Monthly", "On-Demand", etc.)
```

### ProviderIdentity
```
Email: string?
Plan: string? ("Pro", "Free", "Team", etc.)
Organization: string?
```

---

## Status Page Monitoring

**Statuspage.io Format:**
```
GET {status_url}/api/v2/status.json

Response:
{
  "status": {
    "indicator": "none|minor|major|critical",
    "description": "All Systems Operational"
  }
}
```

**Status URLs:**
- Claude: https://status.claude.com
- OpenAI: https://status.openai.com
- GitHub: https://www.githubstatus.com
- Cursor: https://status.cursor.com
- Factory: https://status.factory.ai

---

## Environment Variables Reference

```bash
# Claude
CLAUDE_AUTH_TOKEN=sk-ant-...

# Copilot
COPILOT_API_TOKEN=ghp_...
GITHUB_TOKEN=ghp_...

# MiniMax
MINIMAX_API_TOKEN=...
MINIMAX_COOKIE="..."
MINIMAX_AUTHORIZATION="Bearer ..."
MINIMAX_GROUP_ID=...
MINIMAX_HOST_OVERRIDE=...

# Kimi
KIMI_AUTH_TOKEN=...
KIMI_K2_API_KEY=...

# z.ai
ZAI_API_TOKEN=...

# VertexAI
GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json

# OpenAI/Codex
OPENAI_API_KEY=sk-...
```

---

## Windows-Specific Paths

| macOS Path | Windows Equivalent |
|------------|-------------------|
| `~/.claude/` | `%USERPROFILE%\.claude\` |
| `~/.codex/` | `%USERPROFILE%\.codex\` |
| `~/.config/gcloud/` | `%APPDATA%\gcloud\` |
| `~/Library/Application Support/` | `%APPDATA%\` |

---

## Cookie Extraction (Windows)

**Chrome:**
- Path: `%LOCALAPPDATA%\Google\Chrome\User Data\Default\Network\Cookies`
- Encryption: DPAPI (use `System.Security.Cryptography.ProtectedData`)

**Edge:**
- Path: `%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Network\Cookies`
- Encryption: Same as Chrome (DPAPI)

**Firefox:**
- Path: `%APPDATA%\Mozilla\Firefox\Profiles\{profile}\cookies.sqlite`
- Encryption: None (plain SQLite)

---

## Error Handling

| HTTP Status | Meaning | Action |
|-------------|---------|--------|
| 401 | Unauthorized | Clear credentials, re-auth |
| 403 | Forbidden | Check scopes, re-auth |
| 429 | Rate limited | Back off, retry later |
| 5xx | Server error | Retry with backoff |

---

## Refresh Strategy

1. **Cache Duration:** 30 seconds minimum between API calls
2. **Background Refresh:** Configurable interval (1m, 2m, 5m, 15m)
3. **On-Demand:** Manual refresh via UI button
4. **Stale Data:** Show cached with indicator if fetch fails

---

## Feature Checklist for Windows Port

- [x] Claude OAuth
- [x] Claude Web (cookies)
- [x] Codex OAuth
- [x] Cursor (cookies)
- [ ] Copilot (device flow)
- [ ] Gemini (gcloud OAuth)
- [ ] Kimi (JWT auth)
- [ ] Kimi K2 (API key)
- [ ] z.ai (API token)
- [ ] MiniMax (cookies/API)
- [ ] Augment (cookies)
- [ ] Amp (cookies)
- [ ] JetBrains (local XML)
- [ ] Kiro (CLI)
- [ ] VertexAI (gcloud)
- [ ] Factory (WorkOS)
- [ ] OpenCode (cookies)
- [ ] Status page monitoring
- [ ] Cost tracking

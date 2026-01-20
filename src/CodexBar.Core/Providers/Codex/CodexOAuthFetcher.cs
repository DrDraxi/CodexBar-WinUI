using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Debug;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Codex;

/// <summary>
/// Fetches Codex usage via OAuth credentials from ~/.codex/auth.json
/// </summary>
public class CodexOAuthFetcher : IProviderFetcher
{
    private static readonly string CredentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codex",
        "auth.json"
    );

    private static readonly string UsageEndpoint = "https://chatgpt.com/backend-api/wham/usage";
    private static readonly string RefreshEndpoint = "https://auth.openai.com/oauth/token";
    private static readonly string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";

    public UsageProvider Provider => UsageProvider.Codex;

    public Task<bool> IsAvailableAsync()
    {
        var exists = File.Exists(CredentialsPath);
        DebugLog.Log("CodexOAuth", $"IsAvailable: credentialsPath={CredentialsPath}, exists={exists}");
        return Task.FromResult(exists);
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var credentials = await LoadCredentialsAsync();
            if (credentials == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Codex,
                    Error = "No Codex OAuth credentials found"
                };
            }

            // Check if refresh is needed (8+ days old)
            if (credentials.NeedsRefresh && !string.IsNullOrEmpty(credentials.RefreshToken))
            {
                credentials = await RefreshTokenAsync(credentials);
                if (credentials != null)
                {
                    await SaveCredentialsAsync(credentials);
                }
            }

            if (credentials == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Codex,
                    Error = "Failed to refresh token"
                };
            }

            return await FetchUsageAsync(credentials);
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Error = $"Codex OAuth error: {ex.Message}"
            };
        }
    }

    private async Task<CodexCredentials?> LoadCredentialsAsync()
    {
        if (!File.Exists(CredentialsPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(CredentialsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for legacy OPENAI_API_KEY
            if (root.TryGetProperty("OPENAI_API_KEY", out var apiKey))
            {
                var key = apiKey.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return new CodexCredentials
                    {
                        AccessToken = key,
                        RefreshToken = "",
                        IsLegacyApiKey = true
                    };
                }
            }

            // OAuth tokens structure
            if (root.TryGetProperty("tokens", out var tokens))
            {
                var accessToken = tokens.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                var refreshToken = tokens.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
                var idToken = tokens.TryGetProperty("id_token", out var it) ? it.GetString() : null;
                var accountId = tokens.TryGetProperty("account_id", out var ai) ? ai.GetString() : null;

                DateTime? lastRefresh = null;
                if (root.TryGetProperty("last_refresh", out var lr))
                {
                    if (DateTime.TryParse(lr.GetString(), out var parsed))
                    {
                        lastRefresh = parsed;
                    }
                }

                if (!string.IsNullOrEmpty(accessToken))
                {
                    return new CodexCredentials
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken ?? "",
                        IdToken = idToken,
                        AccountId = accountId,
                        LastRefresh = lastRefresh
                    };
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<CodexCredentials?> RefreshTokenAsync(CodexCredentials credentials)
    {
        if (string.IsNullOrEmpty(credentials.RefreshToken))
            return credentials;

        try
        {
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = credentials.RefreshToken,
                ["scope"] = "openid profile email"
            });

            var response = await client.PostAsync(RefreshEndpoint, content);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new CodexCredentials
            {
                AccessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() ?? credentials.AccessToken : credentials.AccessToken,
                RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? credentials.RefreshToken : credentials.RefreshToken,
                IdToken = root.TryGetProperty("id_token", out var it) ? it.GetString() : credentials.IdToken,
                AccountId = credentials.AccountId,
                LastRefresh = DateTime.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveCredentialsAsync(CodexCredentials credentials)
    {
        try
        {
            Dictionary<string, object> json = new();

            // Try to preserve existing data
            if (File.Exists(CredentialsPath))
            {
                var existing = await File.ReadAllTextAsync(CredentialsPath);
                json = JsonSerializer.Deserialize<Dictionary<string, object>>(existing) ?? new();
            }

            json["tokens"] = new Dictionary<string, string?>
            {
                ["access_token"] = credentials.AccessToken,
                ["refresh_token"] = credentials.RefreshToken,
                ["id_token"] = credentials.IdToken,
                ["account_id"] = credentials.AccountId
            };
            json["last_refresh"] = DateTime.UtcNow.ToString("O");

            var jsonString = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(CredentialsPath, jsonString);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private async Task<UsageSnapshot> FetchUsageAsync(CodexCredentials credentials)
    {
        DebugLog.Log("CodexOAuth", $"Fetching usage with token (first 20): {credentials.AccessToken.Substring(0, Math.Min(20, credentials.AccessToken.Length))}...");
        DebugLog.Log("CodexOAuth", $"AccountId: {credentials.AccountId ?? "null"}, IsLegacy: {credentials.IsLegacyApiKey}");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        client.DefaultRequestHeaders.Add("User-Agent", "CodexBar");

        if (!string.IsNullOrEmpty(credentials.AccountId))
        {
            client.DefaultRequestHeaders.Add("ChatGPT-Account-Id", credentials.AccountId);
        }

        var response = await client.GetAsync(UsageEndpoint);
        var responseBody = await response.Content.ReadAsStringAsync();
        DebugLog.Log("CodexOAuth", $"Response: {response.StatusCode}, Body (truncated): {responseBody.Substring(0, Math.Min(200, responseBody.Length))}");

        if (!response.IsSuccessStatusCode)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Error = $"API error: {response.StatusCode}"
            };
        }

        return ParseUsageResponse(responseBody);
    }

    private UsageSnapshot ParseUsageResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            RateWindow? primary = null;
            RateWindow? secondary = null;
            string? planType = null;

            // Get plan type
            if (root.TryGetProperty("plan_type", out var pt))
            {
                planType = pt.GetString();
            }

            // Parse rate_limit (singular) with primary_window format
            if (root.TryGetProperty("rate_limit", out var rateLimit))
            {
                if (rateLimit.TryGetProperty("primary_window", out var primaryWindow))
                {
                    var usedPercent = primaryWindow.TryGetProperty("used_percent", out var up) ? up.GetDouble() : 0;
                    var windowSeconds = primaryWindow.TryGetProperty("limit_window_seconds", out var lws) ? lws.GetInt32() : 0;
                    var resetAfterSeconds = primaryWindow.TryGetProperty("reset_after_seconds", out var ras) ? ras.GetInt32() : 0;

                    primary = new RateWindow
                    {
                        UsedPercent = usedPercent,
                        WindowMinutes = windowSeconds / 60,
                        ResetsAt = resetAfterSeconds > 0 ? DateTime.UtcNow.AddSeconds(resetAfterSeconds) : null
                    };
                }

                // Check for secondary window if exists
                if (rateLimit.TryGetProperty("secondary_window", out var secondaryWindow))
                {
                    var usedPercent = secondaryWindow.TryGetProperty("used_percent", out var up) ? up.GetDouble() : 0;
                    var windowSeconds = secondaryWindow.TryGetProperty("limit_window_seconds", out var lws) ? lws.GetInt32() : 0;
                    var resetAfterSeconds = secondaryWindow.TryGetProperty("reset_after_seconds", out var ras) ? ras.GetInt32() : 0;

                    secondary = new RateWindow
                    {
                        UsedPercent = usedPercent,
                        WindowMinutes = windowSeconds / 60,
                        ResetsAt = resetAfterSeconds > 0 ? DateTime.UtcNow.AddSeconds(resetAfterSeconds) : null
                    };
                }
            }

            // Parse credits
            ProviderCostSnapshot? creditsSnapshot = null;
            if (root.TryGetProperty("credits", out var credits))
            {
                var hasCredits = credits.TryGetProperty("has_credits", out var hc) && hc.GetBoolean();
                var unlimited = credits.TryGetProperty("unlimited", out var ul) && ul.GetBoolean();
                double? balance = null;

                if (credits.TryGetProperty("balance", out var bal))
                {
                    if (bal.ValueKind == JsonValueKind.Number)
                    {
                        balance = bal.GetDouble();
                    }
                    else if (bal.ValueKind == JsonValueKind.String && double.TryParse(bal.GetString(), out var parsed))
                    {
                        balance = parsed;
                    }
                }

                DebugLog.Log("CodexOAuth", $"Credits: hasCredits={hasCredits}, unlimited={unlimited}, balance={balance}");

                if (hasCredits && balance.HasValue)
                {
                    creditsSnapshot = new ProviderCostSnapshot
                    {
                        Used = 0,
                        Limit = balance.Value,
                        CurrencyCode = "Credits",
                        Period = unlimited ? "Unlimited" : "Credits"
                    };
                }
            }

            // Fallback: Parse rate_limits (plural) format
            if (primary == null && root.TryGetProperty("rate_limits", out var rateLimits))
            {
                if (rateLimits.TryGetProperty("gpt4", out var gpt4) ||
                    rateLimits.TryGetProperty("default", out gpt4))
                {
                    primary = ParseRateLimit(gpt4);
                }
                if (rateLimits.TryGetProperty("gpt4o", out var gpt4o))
                {
                    secondary = ParseRateLimit(gpt4o);
                }
            }

            // Fallback: direct usage fields
            if (primary == null && root.TryGetProperty("usage", out var usage))
            {
                var used = usage.TryGetProperty("used", out var u) ? u.GetDouble() : 0;
                var limit = usage.TryGetProperty("limit", out var l) ? l.GetDouble() : 100;
                primary = new RateWindow
                {
                    UsedPercent = limit > 0 ? (used / limit) * 100 : 0
                };
            }

            DebugLog.Log("CodexOAuth", $"Parsed: planType={planType}, primary={primary?.UsedPercent}%, secondary={secondary?.UsedPercent}%, credits={creditsSnapshot?.Limit}");

            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Primary = primary,
                Secondary = secondary,
                ProviderCost = creditsSnapshot,
                Identity = new ProviderIdentity
                {
                    Plan = planType ?? "Unknown"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("CodexOAuth", $"Parse error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Error = $"Parse error: {ex.Message}"
            };
        }
    }

    private RateWindow? ParseRateLimit(JsonElement element)
    {
        try
        {
            var used = element.TryGetProperty("used", out var u) ? u.GetDouble() : 0;
            var limit = element.TryGetProperty("limit", out var l) ? l.GetDouble() : 0;

            DateTime? resetsAt = null;
            if (element.TryGetProperty("resets_at", out var ra))
            {
                if (DateTime.TryParse(ra.GetString(), out var parsed))
                {
                    resetsAt = parsed;
                }
            }

            int? windowMinutes = null;
            if (element.TryGetProperty("window_minutes", out var wm))
            {
                windowMinutes = wm.GetInt32();
            }

            return new RateWindow
            {
                UsedPercent = limit > 0 ? (used / limit) * 100 : 0,
                WindowMinutes = windowMinutes,
                ResetsAt = resetsAt
            };
        }
        catch
        {
            return null;
        }
    }

    private class CodexCredentials
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string? IdToken { get; set; }
        public string? AccountId { get; set; }
        public DateTime? LastRefresh { get; set; }
        public bool IsLegacyApiKey { get; set; }

        public bool NeedsRefresh
        {
            get
            {
                if (IsLegacyApiKey) return false;
                if (LastRefresh == null) return true;
                var eightDays = TimeSpan.FromDays(8);
                return DateTime.UtcNow - LastRefresh.Value > eightDays;
            }
        }
    }
}

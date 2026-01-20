using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Debug;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Fetches Claude usage data via web API using browser cookies
/// </summary>
public class ClaudeWebFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient;
    private const string ClaudeApiBase = "https://claude.ai/api";

    static ClaudeWebFetcher()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        HttpClient = new HttpClient(handler);
        HttpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        HttpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
        HttpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
        HttpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        HttpClient.DefaultRequestHeaders.Add("Origin", "https://claude.ai");
        HttpClient.DefaultRequestHeaders.Add("Referer", "https://claude.ai/");
    }

    public UsageProvider Provider => UsageProvider.Claude;

    public async Task<bool> IsAvailableAsync()
    {
        var sessionKey = await GetSessionKeyAsync();
        return sessionKey != null;
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var sessionKey = await GetSessionKeyAsync();
            if (sessionKey == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Claude,
                    Error = "No sessionKey cookie found in browser"
                };
            }

            // Get organization ID first
            var orgId = await GetOrganizationIdAsync(sessionKey);
            if (orgId == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Claude,
                    Error = "Could not get organization ID"
                };
            }

            // Fetch usage
            var usage = await FetchUsageAsync(sessionKey, orgId);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Claude,
                    Error = "Failed to fetch usage data"
                };
            }

            // Fetch account info
            var account = await FetchAccountAsync(sessionKey);

            return new UsageSnapshot
            {
                Provider = UsageProvider.Claude,
                Primary = usage.SessionUsedPercent != null ? new RateWindow
                {
                    UsedPercent = usage.SessionUsedPercent.Value,
                    WindowMinutes = 300,
                    ResetsAt = usage.SessionWindowRemaining != null
                        ? DateTime.UtcNow.AddSeconds(usage.SessionWindowRemaining.Value)
                        : null
                } : null,
                Secondary = usage.WeeklyUsedPercent != null ? new RateWindow
                {
                    UsedPercent = usage.WeeklyUsedPercent.Value,
                    WindowMinutes = 10080,
                    ResetsAt = usage.WeeklyWindowRemaining != null
                        ? DateTime.UtcNow.AddSeconds(usage.WeeklyWindowRemaining.Value)
                        : null
                } : null,
                Tertiary = usage.OpusUsedPercent != null ? new RateWindow
                {
                    UsedPercent = usage.OpusUsedPercent.Value,
                    WindowMinutes = 10080
                } : null,
                Identity = new ProviderIdentity
                {
                    Email = account?.Email,
                    Plan = "Pro (Web)"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Claude,
                Error = ex.Message
            };
        }
    }

    private static async Task<string?> GetSessionKeyAsync()
    {
        // First check for manual cookie
        var manualCookie = ManualCookieStore.GetCookie("Claude");
        if (!string.IsNullOrEmpty(manualCookie))
        {
            // Extract sessionKey from cookie string
            var sessionKey = ExtractCookieValue(manualCookie, "sessionKey");
            if (!string.IsNullOrEmpty(sessionKey))
            {
                return sessionKey;
            }
            // If manual cookie doesn't have sessionKey, return the whole thing
            return manualCookie;
        }

        // Try to get sessionKey from Chrome/Edge cookies
        return await ChromeCookieExtractor.GetCookieAsync("claude.ai", "sessionKey");
    }

    /// <summary>
    /// Get full cookie string including Cloudflare cookies
    /// </summary>
    private static async Task<string> GetFullCookieStringAsync(string sessionKey)
    {
        var cookies = new List<string> { $"sessionKey={sessionKey}" };

        // Try to get Cloudflare clearance cookie
        var cfClearance = await ChromeCookieExtractor.GetCookieAsync("claude.ai", "cf_clearance");
        if (!string.IsNullOrEmpty(cfClearance))
        {
            cookies.Add($"cf_clearance={cfClearance}");
        }

        // Try to get __cf_bm cookie
        var cfBm = await ChromeCookieExtractor.GetCookieAsync("claude.ai", "__cf_bm");
        if (!string.IsNullOrEmpty(cfBm))
        {
            cookies.Add($"__cf_bm={cfBm}");
        }

        return string.Join("; ", cookies);
    }

    private static string? ExtractCookieValue(string cookieString, string name)
    {
        if (string.IsNullOrEmpty(cookieString))
            return null;

        foreach (var part in cookieString.Split(';'))
        {
            var trimmed = part.Trim();
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx > 0)
            {
                var cookieName = trimmed.Substring(0, eqIdx).Trim();
                if (cookieName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(eqIdx + 1).Trim();
                }
            }
        }

        return null;
    }

    private static async Task<string?> GetOrganizationIdAsync(string sessionKey)
    {
        DebugLog.Log("Claude", $"Getting organizations with sessionKey: {sessionKey.Substring(0, Math.Min(20, sessionKey.Length))}...");

        // Get full cookie string including Cloudflare cookies
        var cookieString = await GetFullCookieStringAsync(sessionKey);
        DebugLog.Log("Claude", $"Cookie string length: {cookieString.Length}");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClaudeApiBase}/organizations");
        request.Headers.Add("Cookie", cookieString);

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        DebugLog.Log("Claude", $"Organizations API: {response.StatusCode}");
        DebugLog.Log("Claude", $"Response (truncated): {json.Substring(0, Math.Min(500, json.Length))}");

        if (!response.IsSuccessStatusCode)
        {
            // Check if it's a Cloudflare challenge
            if (json.Contains("Just a moment"))
            {
                DebugLog.Log("Claude", "Cloudflare challenge detected - web API blocked");
            }
            else
            {
                DebugLog.Log("Claude", $"Error getting orgs: {response.StatusCode}");
            }
            return null;
        }

        try
        {
            var orgs = JsonSerializer.Deserialize<List<ClaudeOrganization>>(json);
            var orgId = orgs?.FirstOrDefault()?.Uuid;
            DebugLog.Log("Claude", $"Found org ID: {orgId ?? "null"}");
            return orgId;
        }
        catch (Exception ex)
        {
            DebugLog.Log("Claude", $"Parse error: {ex.Message}");
            return null;
        }
    }

    private static async Task<ClaudeWebUsageResponse?> FetchUsageAsync(string sessionKey, string orgId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClaudeApiBase}/organizations/{orgId}/usage");
        request.Headers.Add("Cookie", $"sessionKey={sessionKey}");

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ClaudeWebUsageResponse>(json);
    }

    private static async Task<ClaudeAccountResponse?> FetchAccountAsync(string sessionKey)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClaudeApiBase}/account");
            request.Headers.Add("Cookie", $"sessionKey={sessionKey}");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ClaudeAccountResponse>(json);
        }
        catch
        {
            return null;
        }
    }
}

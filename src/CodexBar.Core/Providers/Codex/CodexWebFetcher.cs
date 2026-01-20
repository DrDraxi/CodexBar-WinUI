using System.Net;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Codex;

/// <summary>
/// Fetches Codex/ChatGPT usage via browser cookies
/// </summary>
public class CodexWebFetcher : IProviderFetcher
{
    private static readonly string UsageEndpoint = "https://chatgpt.com/backend-api/wham/usage";
    private static readonly string MeEndpoint = "https://chatgpt.com/backend-api/me";

    public UsageProvider Provider => UsageProvider.Codex;

    public async Task<bool> IsAvailableAsync()
    {
        // Check for manual cookie first
        if (ManualCookieStore.HasCookie("Codex"))
            return true;

        // Check if we have chatgpt.com cookies
        var sessionToken = await ChromeCookieExtractor.GetCookieAsync("chatgpt.com", "__Secure-next-auth.session-token");
        return !string.IsNullOrEmpty(sessionToken);
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var cookies = await GetCookiesAsync();
            if (string.IsNullOrEmpty(cookies))
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Codex,
                    Error = "No ChatGPT cookies found"
                };
            }

            return await FetchUsageWithCookiesAsync(cookies);
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Error = $"Codex web error: {ex.Message}"
            };
        }
    }

    private async Task<string?> GetCookiesAsync()
    {
        // First check for manual cookie
        var manualCookie = ManualCookieStore.GetCookie("Codex");
        if (!string.IsNullOrEmpty(manualCookie))
        {
            return manualCookie;
        }

        // Get session token from browser
        var sessionToken = await ChromeCookieExtractor.GetCookieAsync("chatgpt.com", "__Secure-next-auth.session-token");
        if (string.IsNullOrEmpty(sessionToken))
            return null;

        // Build cookie string
        var cookies = new List<string>
        {
            $"__Secure-next-auth.session-token={sessionToken}"
        };

        // Try to get additional cookies that might be needed
        var cfClearance = await ChromeCookieExtractor.GetCookieAsync("chatgpt.com", "cf_clearance");
        if (!string.IsNullOrEmpty(cfClearance))
        {
            cookies.Add($"cf_clearance={cfClearance}");
        }

        return string.Join("; ", cookies);
    }

    private async Task<UsageSnapshot> FetchUsageWithCookiesAsync(string cookies)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false
        };

        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Cookie", cookies);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await client.GetAsync(UsageEndpoint);
        if (!response.IsSuccessStatusCode)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Error = $"API error: {response.StatusCode}"
            };
        }

        var json = await response.Content.ReadAsStringAsync();
        return ParseUsageResponse(json);
    }

    private UsageSnapshot ParseUsageResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            RateWindow? primary = null;
            RateWindow? secondary = null;

            // Parse different response formats
            if (root.TryGetProperty("rate_limits", out var rateLimits))
            {
                foreach (var prop in rateLimits.EnumerateObject())
                {
                    var window = ParseRateLimit(prop.Value);
                    if (window != null)
                    {
                        if (primary == null)
                            primary = window;
                        else if (secondary == null)
                            secondary = window;
                    }
                }
            }

            // Alternative: usage object
            if (primary == null && root.TryGetProperty("usage", out var usage))
            {
                primary = ParseRateLimit(usage);
            }

            // Alternative: direct fields
            if (primary == null)
            {
                var used = root.TryGetProperty("used", out var u) ? u.GetDouble() : 0;
                var limit = root.TryGetProperty("limit", out var l) ? l.GetDouble() : 100;
                if (limit > 0)
                {
                    primary = new RateWindow
                    {
                        UsedPercent = (used / limit) * 100
                    };
                }
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Codex,
                Primary = primary,
                Secondary = secondary,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
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
            double usedPercent = 0;

            if (element.TryGetProperty("used_percent", out var up))
            {
                usedPercent = up.GetDouble();
            }
            else if (element.TryGetProperty("used", out var used) && element.TryGetProperty("limit", out var limit))
            {
                var usedVal = used.GetDouble();
                var limitVal = limit.GetDouble();
                if (limitVal > 0)
                {
                    usedPercent = (usedVal / limitVal) * 100;
                }
            }

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
                UsedPercent = usedPercent,
                WindowMinutes = windowMinutes,
                ResetsAt = resetsAt
            };
        }
        catch
        {
            return null;
        }
    }
}

using System.Text;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Kimi;

/// <summary>
/// Fetches Kimi usage data via browser cookies
/// </summary>
public class KimiFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string UsageUrl = "https://kimi.com/api/user/usage";

    // Cookie names to look for
    private static readonly string[] CookieNames =
    [
        "kimi_session",
        "session",
        "__session"
    ];

    public UsageProvider Provider => UsageProvider.Kimi;

    public async Task<bool> IsAvailableAsync()
    {
        // Check for manual cookie first
        if (ManualCookieStore.HasCookie("Kimi"))
            return true;

        var cookie = await GetSessionCookieAsync();
        return cookie != null;
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var cookie = await GetSessionCookieAsync();
            if (cookie == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Kimi,
                    Error = "No Kimi session cookie found. Sign in to kimi.com in Chrome/Edge."
                };
            }

            var usage = await FetchUsageAsync(cookie.Value.name, cookie.Value.value);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Kimi,
                    Error = "Failed to fetch usage data"
                };
            }

            DebugLog.Log("Kimi", $"Weekly: used={usage.WeeklyUsed}, limit={usage.WeeklyLimit}, percent={usage.WeeklyPercentUsed:F1}%");
            DebugLog.Log("Kimi", $"Rate: used={usage.RateUsed}, limit={usage.RateLimit}, percent={usage.RatePercentUsed:F1}%");

            return new UsageSnapshot
            {
                Provider = UsageProvider.Kimi,
                Primary = new RateWindow
                {
                    Label = "Weekly",
                    UsedPercent = usage.WeeklyPercentUsed
                },
                Secondary = usage.HasRateLimit ? new RateWindow
                {
                    Label = "5h Rate",
                    UsedPercent = usage.RatePercentUsed
                } : null,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("Kimi", $"Error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Kimi,
                Error = ex.Message
            };
        }
    }

    private static async Task<(string name, string value)?> GetSessionCookieAsync()
    {
        // Check for manual cookie first
        var manualCookie = ManualCookieStore.GetCookie("Kimi");
        if (!string.IsNullOrEmpty(manualCookie))
        {
            // Try to extract a known cookie name from the string
            foreach (var cookieName in CookieNames)
            {
                var extracted = ExtractCookieValue(manualCookie, cookieName);
                if (!string.IsNullOrEmpty(extracted))
                {
                    return (cookieName, extracted);
                }
            }
            // If no known cookie found, use the whole string with a generic name
            return ("Cookie", manualCookie);
        }

        // Try browser cookies
        foreach (var cookieName in CookieNames)
        {
            var value = await ChromeCookieExtractor.GetCookieAsync("kimi.com", cookieName);
            if (!string.IsNullOrEmpty(value))
            {
                return (cookieName, value);
            }
        }
        return null;
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

    private static async Task<KimiUsageResponse?> FetchUsageAsync(string cookieName, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, UsageUrl);
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        DebugLog.Log("Kimi", $"API response: {response.StatusCode}");
        DebugLog.Log("Kimi", $"Response body (truncated): {json.Substring(0, Math.Min(500, json.Length))}");

        if (!response.IsSuccessStatusCode) return null;

        return JsonSerializer.Deserialize<KimiUsageResponse>(json);
    }
}

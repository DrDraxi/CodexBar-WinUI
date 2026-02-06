using System.Text;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.OpenCode;

/// <summary>
/// Fetches OpenCode usage data via browser cookies
/// </summary>
public class OpenCodeFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string UsageUrl = "https://opencode.ai/_server";

    // Cookie names to look for
    private static readonly string[] CookieNames =
    [
        "session",
        "__session",
        "oc_session"
    ];

    public UsageProvider Provider => UsageProvider.OpenCode;

    public async Task<bool> IsAvailableAsync()
    {
        if (ManualCookieStore.HasCookie("OpenCode"))
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
                    Provider = UsageProvider.OpenCode,
                    Error = "No OpenCode session cookie found. Sign in to opencode.ai in Chrome/Edge."
                };
            }

            var usage = await FetchUsageAsync(cookie.Value.name, cookie.Value.value);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.OpenCode,
                    Error = "Failed to fetch usage data"
                };
            }

            DebugLog.Log("OpenCode", $"5h={usage.FiveHourPercent}%, weekly={usage.WeeklyPercent}%, plan={usage.Plan}");

            return new UsageSnapshot
            {
                Provider = UsageProvider.OpenCode,
                Primary = new RateWindow
                {
                    Label = "5h",
                    UsedPercent = usage.FiveHourPercent ?? 0
                },
                Secondary = new RateWindow
                {
                    Label = "Weekly",
                    UsedPercent = usage.WeeklyPercent ?? 0
                },
                Identity = new ProviderIdentity
                {
                    Plan = usage.Plan
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("OpenCode", $"Fetch error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.OpenCode,
                Error = ex.Message
            };
        }
    }

    private static async Task<(string name, string value)?> GetSessionCookieAsync()
    {
        // Check for manual cookie first
        var manualCookie = ManualCookieStore.GetCookie("OpenCode");
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
            var value = await ChromeCookieExtractor.GetCookieAsync("opencode.ai", cookieName);
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

    private static async Task<OpenCodeUsageResponse?> FetchUsageAsync(string cookieName, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, UsageUrl);
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { fn = "getUsage" }),
            Encoding.UTF8,
            "application/json");

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        DebugLog.Log("OpenCode", $"API response: {response.StatusCode}");
        DebugLog.Log("OpenCode", $"Response body (truncated): {json.Substring(0, Math.Min(500, json.Length))}");

        if (!response.IsSuccessStatusCode) return null;

        return JsonSerializer.Deserialize<OpenCodeUsageResponse>(json);
    }
}

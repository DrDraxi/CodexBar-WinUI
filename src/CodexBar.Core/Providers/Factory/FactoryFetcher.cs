using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Factory;

/// <summary>
/// Fetches Factory (Droid) usage data via browser cookies
/// </summary>
public class FactoryFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string UsageUrl = "https://app.factory.ai/api/usage";

    // Cookie names to look for
    private static readonly string[] CookieNames =
    [
        "session",
        "__session",
        "factory_session"
    ];

    public UsageProvider Provider => UsageProvider.Factory;

    public async Task<bool> IsAvailableAsync()
    {
        // Check for manual cookie first
        if (ManualCookieStore.HasCookie("Factory"))
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
                    Provider = UsageProvider.Factory,
                    Error = "No Factory session cookie found. Sign in to app.factory.ai in Chrome/Edge."
                };
            }

            var usage = await FetchUsageAsync(cookie.Value.name, cookie.Value.value);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Factory,
                    Error = "Failed to fetch usage data"
                };
            }

            DebugLog.Log("Factory", $"Standard: used={usage.Standard?.Used}, limit={usage.Standard?.Limit}, percent={usage.Standard?.PercentUsed:F1}%");
            DebugLog.Log("Factory", $"Premium: used={usage.Premium?.Used}, limit={usage.Premium?.Limit}, percent={usage.Premium?.PercentUsed:F1}%");

            return new UsageSnapshot
            {
                Provider = UsageProvider.Factory,
                Primary = usage.Standard != null ? new RateWindow
                {
                    Label = "Standard",
                    UsedPercent = usage.Standard.PercentUsed
                } : null,
                Secondary = usage.Premium != null ? new RateWindow
                {
                    Label = "Premium",
                    UsedPercent = usage.Premium.PercentUsed
                } : null,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("Factory", $"Error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Factory,
                Error = ex.Message
            };
        }
    }

    private static async Task<(string name, string value)?> GetSessionCookieAsync()
    {
        // Check for manual cookie first
        var manualCookie = ManualCookieStore.GetCookie("Factory");
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
            var value = await ChromeCookieExtractor.GetCookieAsync("app.factory.ai", cookieName);
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

    private static async Task<FactoryUsageResponse?> FetchUsageAsync(string cookieName, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        DebugLog.Log("Factory", $"API response: {response.StatusCode}");
        DebugLog.Log("Factory", $"Response body (truncated): {json.Substring(0, Math.Min(500, json.Length))}");

        if (!response.IsSuccessStatusCode) return null;

        return JsonSerializer.Deserialize<FactoryUsageResponse>(json);
    }
}

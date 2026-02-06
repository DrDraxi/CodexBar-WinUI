using System.Text.Json;
using System.Text.RegularExpressions;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Amp;

/// <summary>
/// Fetches Amp usage data by scraping the settings page for freeTierUsage JSON
/// </summary>
public class AmpFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string SettingsUrl = "https://ampcode.com/settings";

    // Regex to find the freeTierUsage JSON object embedded in the page
    private static readonly Regex FreeTierUsageRegex = new(
        @"""freeTierUsage""\s*:\s*(\{[^}]+\})",
        RegexOptions.Compiled);

    // Cookie names to look for
    private static readonly string[] CookieNames =
    [
        "_session",
        "session",
        "amp_session"
    ];

    public UsageProvider Provider => UsageProvider.Amp;

    public async Task<bool> IsAvailableAsync()
    {
        // Check for manual cookie first
        if (ManualCookieStore.HasCookie("Amp"))
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
                    Provider = UsageProvider.Amp,
                    Error = "No Amp session cookie found. Sign in to ampcode.com in Chrome/Edge."
                };
            }

            var freeTier = await ScrapeUsageAsync(cookie.Value.name, cookie.Value.value);
            if (freeTier == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Amp,
                    Error = "Could not find usage data on settings page"
                };
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Amp,
                Primary = new RateWindow
                {
                    UsedPercent = freeTier.PercentUsed,
                    ResetsAt = freeTier.ResetsAtParsed,
                    Label = "Daily"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("Amp", $"Fetch error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Amp,
                Error = ex.Message
            };
        }
    }

    private static async Task<(string name, string value)?> GetSessionCookieAsync()
    {
        // Check for manual cookie first
        var manualCookie = ManualCookieStore.GetCookie("Amp");
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
            var value = await ChromeCookieExtractor.GetCookieAsync("ampcode.com", cookieName);
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

    private static async Task<AmpFreeTierUsage?> ScrapeUsageAsync(string cookieName, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, SettingsUrl);
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Log("Amp", $"Settings page returned {(int)response.StatusCode}");
            return null;
        }

        var html = await response.Content.ReadAsStringAsync();
        DebugLog.Log("Amp", $"Settings page length: {html.Length} chars");

        var match = FreeTierUsageRegex.Match(html);
        if (!match.Success)
        {
            DebugLog.Log("Amp", "Could not find freeTierUsage JSON in settings page");
            return null;
        }

        var json = match.Groups[1].Value;
        DebugLog.Log("Amp", $"Parsed freeTierUsage: {json}");
        return JsonSerializer.Deserialize<AmpFreeTierUsage>(json);
    }
}

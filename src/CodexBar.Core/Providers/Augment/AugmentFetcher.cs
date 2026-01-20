using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Augment;

/// <summary>
/// Fetches Augment usage data via browser cookies
/// </summary>
public class AugmentFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string CreditsUrl = "https://app.augmentcode.com/api/credits";
    private const string SubscriptionUrl = "https://app.augmentcode.com/api/subscription";

    // Cookie names to look for (Auth0-based)
    private static readonly string[] CookieNames =
    [
        "_session",
        "auth0",
        "__Secure-next-auth.session-token",
        "next-auth.session-token",
        "authjs.session-token"
    ];

    public UsageProvider Provider => UsageProvider.Augment;

    public async Task<bool> IsAvailableAsync()
    {
        // Check for manual cookie first
        if (ManualCookieStore.HasCookie("Augment"))
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
                    Provider = UsageProvider.Augment,
                    Error = "No Augment session cookie found. Sign in to app.augmentcode.com in Chrome/Edge."
                };
            }

            var credits = await FetchCreditsAsync(cookie.Value.name, cookie.Value.value);
            if (credits == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Augment,
                    Error = "Failed to fetch credits data"
                };
            }

            var subscription = await FetchSubscriptionAsync(cookie.Value.name, cookie.Value.value);

            return new UsageSnapshot
            {
                Provider = UsageProvider.Augment,
                Primary = new RateWindow
                {
                    UsedPercent = credits.PercentUsed,
                    ResetsAt = subscription?.BillingPeriodEndParsed
                },
                Identity = new ProviderIdentity
                {
                    Email = subscription?.Email,
                    Organization = subscription?.Organization,
                    Plan = subscription?.PlanName ?? "Augment"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Augment,
                Error = ex.Message
            };
        }
    }

    private static async Task<(string name, string value)?> GetSessionCookieAsync()
    {
        // Check for manual cookie first
        var manualCookie = ManualCookieStore.GetCookie("Augment");
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
            var value = await ChromeCookieExtractor.GetCookieAsync("augmentcode.com", cookieName);
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

    private static async Task<AugmentCreditsResponse?> FetchCreditsAsync(string cookieName, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CreditsUrl);
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AugmentCreditsResponse>(json);
    }

    private static async Task<AugmentSubscriptionResponse?> FetchSubscriptionAsync(string cookieName, string cookieValue)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SubscriptionUrl);
            request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AugmentSubscriptionResponse>(json);
        }
        catch
        {
            return null;
        }
    }
}

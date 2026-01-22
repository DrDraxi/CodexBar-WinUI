using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Cursor;

/// <summary>
/// Fetches Cursor usage data via browser cookies
/// </summary>
public class CursorFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string UsageSummaryUrl = "https://cursor.com/api/usage-summary";
    private const string UserInfoUrl = "https://cursor.com/api/auth/me";

    // Cookie names to look for
    private static readonly string[] CookieNames =
    [
        "WorkosCursorSessionToken",
        "__Secure-next-auth.session-token",
        "next-auth.session-token"
    ];

    public UsageProvider Provider => UsageProvider.Cursor;

    public async Task<bool> IsAvailableAsync()
    {
        // Check for manual cookie first
        if (ManualCookieStore.HasCookie("Cursor"))
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
                    Provider = UsageProvider.Cursor,
                    Error = "No Cursor session cookie found. Sign in to cursor.com in Chrome/Edge."
                };
            }

            var usage = await FetchUsageSummaryAsync(cookie.Value.name, cookie.Value.value);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Cursor,
                    Error = "Failed to fetch usage data"
                };
            }

            var userInfo = await FetchUserInfoAsync(cookie.Value.name, cookie.Value.value);

            // Get usage from individual or team
            var individualUsage = usage.IndividualUsage;
            var planUsage = individualUsage?.Plan ?? usage.TeamUsage?.Plan;
            var onDemand = individualUsage?.OnDemand;

            // Log the data for debugging
            DebugLog.Log("Cursor", $"Plan: used={planUsage?.Used}, limit={planUsage?.Limit}, totalPercent={planUsage?.TotalPercentUsed}");
            DebugLog.Log("Cursor", $"OnDemand: enabled={onDemand?.Enabled}, used={onDemand?.Used} cents");

            // Calculate plan usage - if on-demand is active and has usage, plan is at 100%
            double planPercent = 0;
            if (planUsage != null)
            {
                // If on-demand is enabled and has been used, plan limit is exhausted
                if (onDemand?.Enabled == true && onDemand?.Used > 0)
                {
                    planPercent = 100;
                }
                else
                {
                    planPercent = planUsage.CalculatedPercentUsed;
                }
            }

            // Build on-demand cost snapshot if applicable
            ProviderCostSnapshot? costSnapshot = null;
            if (onDemand?.Enabled == true && onDemand?.Used > 0)
            {
                // On-demand usage is in cents, convert to dollars
                var usedDollars = (onDemand.Used ?? 0) / 100.0;
                // Get limit from API or default to $50
                var limitCents = onDemand.Limit ?? onDemand.HardLimit ?? 5000;
                var limitDollars = limitCents / 100.0;

                costSnapshot = new ProviderCostSnapshot
                {
                    Used = usedDollars,
                    Limit = limitDollars,
                    CurrencyCode = "USD",
                    Period = "On-Demand"
                };
                DebugLog.Log("Cursor", $"OnDemand cost: ${usedDollars:F2} / ${limitDollars:F0}");
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Cursor,
                Primary = planUsage != null ? new RateWindow
                {
                    UsedPercent = planPercent,
                    ResetsAt = usage.BillingCycleEndParsed
                } : null,
                ProviderCost = costSnapshot,
                Identity = new ProviderIdentity
                {
                    Email = userInfo?.Email,
                    Plan = FormatPlanName(usage.MembershipType)
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Cursor,
                Error = ex.Message
            };
        }
    }

    private static async Task<(string name, string value)?> GetSessionCookieAsync()
    {
        // Check for manual cookie first
        var manualCookie = ManualCookieStore.GetCookie("Cursor");
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
            var value = await ChromeCookieExtractor.GetCookieAsync("cursor.com", cookieName);
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

    private static async Task<CursorUsageSummary?> FetchUsageSummaryAsync(string cookieName, string cookieValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageSummaryUrl);
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        DebugLog.Log("Cursor", $"API response: {response.StatusCode}");
        DebugLog.Log("Cursor", $"Response body (truncated): {json.Substring(0, Math.Min(500, json.Length))}");

        if (!response.IsSuccessStatusCode) return null;

        return JsonSerializer.Deserialize<CursorUsageSummary>(json);
    }

    private static async Task<CursorUserInfo?> FetchUserInfoAsync(string cookieName, string cookieValue)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
            request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CursorUserInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatPlanName(string? membershipType)
    {
        return membershipType?.ToLowerInvariant() switch
        {
            "enterprise" => "Enterprise",
            "pro" => "Pro",
            "hobby" => "Hobby",
            _ => membershipType ?? "Cursor"
        };
    }
}

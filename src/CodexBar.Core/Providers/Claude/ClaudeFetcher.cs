using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Debug;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Fetches Claude usage data via OAuth API
/// </summary>
public class ClaudeFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new();
    private const string OAuthUsageUrl = "https://api.anthropic.com/api/oauth/usage";

    public UsageProvider Provider => UsageProvider.Claude;

    public async Task<bool> IsAvailableAsync()
    {
        var credentials = await ClaudeCredentialsLoader.LoadAsync();
        var hasToken = credentials?.AccessToken != null;
        var hasScope = credentials?.HasProfileScope ?? false;
        DebugLog.Log("ClaudeOAuth", $"IsAvailable: hasToken={hasToken}, hasProfileScope={hasScope}, path={ClaudeCredentialsLoader.GetCredentialsPath()}");
        return hasToken && hasScope;
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            DebugLog.Log("ClaudeOAuth", "Starting FetchAsync");
            var credentials = await ClaudeCredentialsLoader.LoadAsync();
            if (credentials?.AccessToken == null)
            {
                var debugPath = ClaudeCredentialsLoader.GetCredentialsPath();
                DebugLog.Log("ClaudeOAuth", $"No credentials found at {debugPath}");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Claude,
                    Error = $"No credentials at {debugPath}"
                };
            }

            DebugLog.Log("ClaudeOAuth", $"Found token (first 20): {credentials.AccessToken.Substring(0, Math.Min(20, credentials.AccessToken.Length))}...");
            DebugLog.Log("ClaudeOAuth", $"Scopes: {string.Join(", ", credentials.Scopes ?? new List<string>())}");

            if (!credentials.HasProfileScope)
            {
                DebugLog.Log("ClaudeOAuth", "Credentials missing user:profile scope");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Claude,
                    Error = "Credentials missing user:profile scope"
                };
            }

            var usage = await FetchOAuthUsageAsync(credentials.AccessToken);
            if (usage == null)
            {
                DebugLog.Log("ClaudeOAuth", "FetchOAuthUsageAsync returned null");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Claude,
                    Error = "Failed to fetch usage data"
                };
            }

            DebugLog.Log("ClaudeOAuth", $"Parsed usage: 5h={usage.FiveHour?.UsagePercent}%, 7d={usage.SevenDay?.UsagePercent}%, opus={usage.SevenDayOpus?.UsagePercent}%");
            DebugLog.Log("ClaudeOAuth", $"Extra usage: spend={usage.ExtraUsage?.Spend}, limit={usage.ExtraUsage?.Limit}");

            // Build extra usage / on-demand cost if available
            ProviderCostSnapshot? extraUsageCost = null;
            if (usage.ExtraUsage?.Spend > 0 || usage.ExtraUsage?.Limit > 0)
            {
                extraUsageCost = new ProviderCostSnapshot
                {
                    Used = usage.ExtraUsage.Spend ?? 0,
                    Limit = usage.ExtraUsage.Limit ?? 0,
                    CurrencyCode = "USD",
                    Period = "Extra Usage"
                };
                DebugLog.Log("ClaudeOAuth", $"Extra usage cost: ${extraUsageCost.Used:F2} / ${extraUsageCost.Limit:F0}");
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Claude,
                Primary = usage.FiveHour != null ? new RateWindow
                {
                    UsedPercent = usage.FiveHour.UsagePercent ?? 0,
                    WindowMinutes = 300, // 5 hours
                    ResetsAt = usage.FiveHour.ResetsAtParsed
                } : null,
                Secondary = usage.SevenDay != null ? new RateWindow
                {
                    UsedPercent = usage.SevenDay.UsagePercent ?? 0,
                    WindowMinutes = 10080, // 7 days
                    ResetsAt = usage.SevenDay.ResetsAtParsed
                } : null,
                Tertiary = usage.SevenDayOpus != null ? new RateWindow
                {
                    UsedPercent = usage.SevenDayOpus.UsagePercent ?? 0,
                    WindowMinutes = 10080,
                    ResetsAt = usage.SevenDayOpus.ResetsAtParsed
                } : null,
                ProviderCost = extraUsageCost,
                Identity = new ProviderIdentity
                {
                    Plan = FormatPlanName(credentials.SubscriptionType)
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

    private static async Task<ClaudeUsageResponse?> FetchOAuthUsageAsync(string accessToken)
    {
        DebugLog.Log("ClaudeOAuth", $"Fetching from {OAuthUsageUrl}");
        using var request = new HttpRequestMessage(HttpMethod.Get, OAuthUsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        DebugLog.Log("ClaudeOAuth", $"Response: {response.StatusCode}, Body (truncated): {json.Substring(0, Math.Min(200, json.Length))}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ClaudeUsageResponse>(json);
    }

    private static string FormatPlanName(string? subscriptionType)
    {
        return subscriptionType?.ToLowerInvariant() switch
        {
            "max" => "Max",
            "pro" => "Pro",
            "free" => "Free",
            "team" => "Team",
            "enterprise" => "Enterprise",
            _ => subscriptionType ?? "Unknown"
        };
    }
}

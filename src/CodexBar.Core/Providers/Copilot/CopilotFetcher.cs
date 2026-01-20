using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Copilot;

/// <summary>
/// Fetches Copilot usage data using stored GitHub token
/// </summary>
public class CopilotFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new();
    private const string UsageUrl = "https://api.github.com/copilot_internal/user";

    public UsageProvider Provider => UsageProvider.Copilot;

    public async Task<bool> IsAvailableAsync()
    {
        var token = await CopilotTokenStore.LoadTokenAsync();
        return token != null;
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var token = await CopilotTokenStore.LoadTokenAsync();
            if (token == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Copilot,
                    Error = "Not logged in. Use Settings to connect GitHub."
                };
            }

            var usage = await FetchUsageAsync(token);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Copilot,
                    Error = "Failed to fetch usage data"
                };
            }

            // Use premium_interactions as primary, chat as secondary
            var primary = usage.QuotaSnapshots?.PremiumInteractions;
            var secondary = usage.QuotaSnapshots?.Chat;

            DateTime? resetDate = null;
            if (!string.IsNullOrEmpty(usage.QuotaResetDate) && DateTime.TryParse(usage.QuotaResetDate, out var parsed))
            {
                resetDate = parsed.ToUniversalTime();
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Copilot,
                Primary = primary != null ? new RateWindow
                {
                    UsedPercent = primary.PercentUsed,
                    ResetsAt = resetDate
                } : null,
                Secondary = secondary != null ? new RateWindow
                {
                    UsedPercent = secondary.PercentUsed,
                    ResetsAt = resetDate
                } : null,
                Identity = new ProviderIdentity
                {
                    Plan = usage.CopilotPlan ?? "Copilot"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Copilot,
                Error = ex.Message
            };
        }
    }

    private static async Task<CopilotUsageResponse?> FetchUsageAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("CodexBar/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token expired or invalid
                CopilotTokenStore.DeleteToken();
            }
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CopilotUsageResponse>(json);
    }
}

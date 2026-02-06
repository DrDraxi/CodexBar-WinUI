using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.MiniMax;

/// <summary>
/// Fetches MiniMax coding plan usage via API token
/// </summary>
public class MiniMaxFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public UsageProvider Provider => UsageProvider.MiniMax;

    public async Task<bool> IsAvailableAsync()
    {
        var token = await GetApiTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var token = await GetApiTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.MiniMax,
                    Error = "No MiniMax API token found. Set MINIMAX_API_TOKEN env var or paste token in settings."
                };
            }

            var host = Environment.GetEnvironmentVariable("MINIMAX_HOST") ?? "platform.minimax.io";
            var url = $"https://{host}/v1/api/openplatform/coding_plan/remains";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                DebugLog.Log("MiniMax", $"API error {response.StatusCode}: {body}");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.MiniMax,
                    Error = $"API returned {(int)response.StatusCode} {response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            DebugLog.Log("MiniMax", $"Response: {json}");

            var result = JsonSerializer.Deserialize<MiniMaxCodingPlanResponse>(json);
            if (result == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.MiniMax,
                    Error = "Failed to parse coding plan data"
                };
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.MiniMax,
                Primary = new RateWindow
                {
                    Label = "Plan",
                    UsedPercent = result.PercentUsed
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("MiniMax", $"Fetch error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.MiniMax,
                Error = ex.Message
            };
        }
    }

    private static Task<string?> GetApiTokenAsync()
    {
        // Check environment variable first
        var envToken = Environment.GetEnvironmentVariable("MINIMAX_API_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
            return Task.FromResult<string?>(envToken);

        // Fall back to manual token store
        var manualToken = ManualCookieStore.GetCookie("MiniMax");
        return Task.FromResult<string?>(manualToken);
    }
}

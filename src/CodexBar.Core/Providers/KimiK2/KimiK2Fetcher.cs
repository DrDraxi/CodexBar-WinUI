using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.KimiK2;

/// <summary>
/// Fetches Kimi K2 usage data via API key
/// </summary>
public class KimiK2Fetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string CreditsUrl = "https://kimi-k2.ai/api/user/credits";

    public UsageProvider Provider => UsageProvider.KimiK2;

    public async Task<bool> IsAvailableAsync()
    {
        var apiKey = await GetApiKeyAsync();
        return !string.IsNullOrEmpty(apiKey);
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.KimiK2,
                    Error = "No Kimi K2 API key found. Set KIMI_K2_API_KEY or KIMI_API_KEY environment variable, or paste a token in settings."
                };
            }

            var credits = await FetchCreditsAsync(apiKey);
            if (credits == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.KimiK2,
                    Error = "Failed to fetch Kimi K2 credits data"
                };
            }

            DebugLog.Log("KimiK2", $"Credits: consumed={credits.Consumed}, remaining={credits.Remaining}, used={credits.PercentUsed:F1}%");

            return new UsageSnapshot
            {
                Provider = UsageProvider.KimiK2,
                Primary = new RateWindow
                {
                    Label = "Credits",
                    UsedPercent = credits.PercentUsed
                },
                Identity = new ProviderIdentity
                {
                    Plan = "Kimi K2"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("KimiK2", $"Error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.KimiK2,
                Error = ex.Message
            };
        }
    }

    private static async Task<string?> GetApiKeyAsync()
    {
        // 1. Check manual token store first
        var manualToken = ManualCookieStore.GetCookie("KimiK2");
        if (!string.IsNullOrEmpty(manualToken))
            return manualToken;

        // 2. Check environment variables
        var apiKey = Environment.GetEnvironmentVariable("KIMI_K2_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
            return apiKey;

        apiKey = Environment.GetEnvironmentVariable("KIMI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
            return apiKey;

        // 3. Check config file at ~/.codexbar/config.json
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codexbar",
                "config.json"
            );

            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("kimi_k2_api_key", out var keyElement))
                {
                    var key = keyElement.GetString();
                    if (!string.IsNullOrEmpty(key))
                        return key;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Log("KimiK2", $"Failed to read config file: {ex.Message}");
        }

        return null;
    }

    private static async Task<KimiK2CreditsResponse?> FetchCreditsAsync(string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CreditsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Log("KimiK2", $"Credits API returned {(int)response.StatusCode} {response.StatusCode}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        DebugLog.Log("KimiK2", $"Credits response: {json}");
        return JsonSerializer.Deserialize<KimiK2CreditsResponse>(json);
    }
}

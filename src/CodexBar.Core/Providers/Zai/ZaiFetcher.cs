using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Auth;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Zai;

/// <summary>
/// Fetches Zai (z.ai) usage data via API token
/// </summary>
public class ZaiFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string UsageUrl = "https://api.z.ai/v1/usage";

    public UsageProvider Provider => UsageProvider.Zai;

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
                    Provider = UsageProvider.Zai,
                    Error = "No Zai API token found. Set ZAI_API_TOKEN env var, add token to ~/.zai/config.json, or paste in settings."
                };
            }

            var usage = await FetchUsageAsync(token);
            if (usage == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Zai,
                    Error = "Failed to fetch Zai usage data"
                };
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Zai,
                Primary = new RateWindow
                {
                    Label = "Tokens",
                    UsedPercent = usage.TokensPercent
                },
                Secondary = usage.HasMcp ? new RateWindow
                {
                    Label = "MCP",
                    UsedPercent = usage.McpPercent
                } : null,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("Zai", $"Fetch error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Zai,
                Error = ex.Message
            };
        }
    }

    private static async Task<string?> GetApiTokenAsync()
    {
        // 1. Check manual token store first
        var manualToken = ManualCookieStore.GetCookie("Zai");
        if (!string.IsNullOrEmpty(manualToken))
            return manualToken;

        // 2. Check environment variable
        var envToken = Environment.GetEnvironmentVariable("ZAI_API_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
            return envToken;

        // 3. Check config file at ~/.zai/config.json
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".zai",
                "config.json"
            );

            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("api_token", out var tokenElement))
                {
                    var token = tokenElement.GetString();
                    if (!string.IsNullOrEmpty(token))
                        return token;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Log("Zai", $"Failed to read config file: {ex.Message}");
        }

        return null;
    }

    private static async Task<ZaiUsageResponse?> FetchUsageAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await HttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Log("Zai", $"API returned {response.StatusCode}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        DebugLog.Log("Zai", $"Usage response: {json}");
        return JsonSerializer.Deserialize<ZaiUsageResponse>(json);
    }
}

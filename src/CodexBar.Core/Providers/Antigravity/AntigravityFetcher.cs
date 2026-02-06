using System.Diagnostics;
using System.Text.Json;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

/// <summary>
/// Experimental provider that checks if the Antigravity IDE is running locally
/// and fetches AI usage data from its local HTTP API.
/// </summary>
public class AntigravityFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private const string UsageUrl = "http://localhost:19234/api/usage";

    public UsageProvider Provider => UsageProvider.Antigravity;

    public async Task<bool> IsAvailableAsync()
    {
        // Check if the Antigravity process is running
        if (IsProcessRunning())
            return true;

        // Also check if the local API port responds
        try
        {
            var response = await HttpClient.GetAsync(UsageUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var processRunning = IsProcessRunning();

            // Try to fetch usage from local API
            HttpResponseMessage response;
            try
            {
                response = await HttpClient.GetAsync(UsageUrl);
            }
            catch (HttpRequestException ex)
            {
                DebugLog.Log("Antigravity", $"Connection failed: {ex.Message}");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Antigravity,
                    Error = processRunning
                        ? "Antigravity is running but local API is not reachable"
                        : "Antigravity IDE not detected"
                };
            }
            catch (TaskCanceledException)
            {
                DebugLog.Log("Antigravity", "Connection timed out");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Antigravity,
                    Error = "Antigravity local API timed out"
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                DebugLog.Log("Antigravity", $"API returned {(int)response.StatusCode}");
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Antigravity,
                    Error = $"Antigravity API returned {(int)response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            DebugLog.Log("Antigravity", $"Usage response: {json}");

            // Parse JSON: { "monthly_used": 500, "monthly_limit": 2000 }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("monthly_used", out var usedElement) ||
                !root.TryGetProperty("monthly_limit", out var limitElement))
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Antigravity,
                    Error = "Failed to parse Antigravity usage response"
                };
            }

            var used = usedElement.GetDouble();
            var limit = limitElement.GetDouble();

            if (limit <= 0)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Antigravity,
                    Error = "Antigravity returned invalid usage limit"
                };
            }

            var percentUsed = (used / limit) * 100.0;

            return new UsageSnapshot
            {
                Provider = UsageProvider.Antigravity,
                Primary = new RateWindow
                {
                    Label = "Monthly",
                    UsedPercent = Math.Min(percentUsed, 100.0)
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log("Antigravity", $"Fetch error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Antigravity,
                Error = ex.Message
            };
        }
    }

    private static bool IsProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("Antigravity");
            var running = processes.Length > 0;

            foreach (var p in processes)
                p.Dispose();

            return running;
        }
        catch (Exception ex)
        {
            DebugLog.Log("Antigravity", $"Process check error: {ex.Message}");
            return false;
        }
    }
}

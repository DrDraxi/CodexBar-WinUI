using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.VertexAI;

/// <summary>
/// Fetches Vertex AI quota usage via Google Cloud Monitoring timeSeries API using ADC credentials.
/// Uses gcloud CLI for authentication and reads project ID from env vars or gcloud config.
/// </summary>
public class VertexAIFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string Tag = "VertexAI";

    public UsageProvider Provider => UsageProvider.VertexAI;

    public async Task<bool> IsAvailableAsync()
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return false;

        var projectId = GetProjectId();
        return projectId != null;
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (accessToken == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.VertexAI,
                    Error = "gcloud CLI not found or failed to get access token. Install gcloud and run 'gcloud auth application-default login'."
                };
            }

            var projectId = GetProjectId();
            if (projectId == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.VertexAI,
                    Error = "No Google Cloud project ID found. Set GOOGLE_CLOUD_PROJECT, GCLOUD_PROJECT, or CLOUDSDK_CORE_PROJECT env var, or configure 'gcloud config set project <id>'."
                };
            }

            DebugLog.Log(Tag, $"Fetching quota for project: {projectId}");

            var (usedPercent, error) = await FetchQuotaUsageAsync(accessToken, projectId);
            if (error != null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.VertexAI,
                    Error = error
                };
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.VertexAI,
                Primary = new RateWindow
                {
                    Label = "Quota",
                    UsedPercent = Math.Min(usedPercent, 100)
                },
                Identity = new ProviderIdentity
                {
                    Organization = projectId,
                    Plan = "Vertex AI"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DebugLog.Log(Tag, $"Error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.VertexAI,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Get access token by running gcloud auth application-default print-access-token
    /// </summary>
    private static async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var result = await RunGcloudCommandAsync("auth application-default print-access-token");
            if (result == null) return null;

            var token = result.Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }
        catch (Exception ex)
        {
            DebugLog.Log(Tag, $"Failed to get access token: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolve the Google Cloud project ID from environment variables or gcloud properties file.
    /// Checks env vars in order: GOOGLE_CLOUD_PROJECT, GCLOUD_PROJECT, CLOUDSDK_CORE_PROJECT.
    /// Falls back to parsing the gcloud properties file (Windows: %APPDATA%/gcloud/properties).
    /// </summary>
    private static string? GetProjectId()
    {
        // Check environment variables in order of precedence
        var envVars = new[] { "GOOGLE_CLOUD_PROJECT", "GCLOUD_PROJECT", "CLOUDSDK_CORE_PROJECT" };
        foreach (var envVar in envVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
            {
                DebugLog.Log(Tag, $"Using project from {envVar}: {value}");
                return value;
            }
        }

        // Try to parse from gcloud properties file
        var propertiesPath = GetGcloudPropertiesPath();
        if (propertiesPath != null && File.Exists(propertiesPath))
        {
            try
            {
                var lines = File.ReadAllLines(propertiesPath);
                var inCoreSection = false;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    if (trimmed.StartsWith("["))
                    {
                        inCoreSection = trimmed.Equals("[core]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (inCoreSection)
                    {
                        var match = Regex.Match(trimmed, @"^project\s*=\s*(.+)$");
                        if (match.Success)
                        {
                            var projectId = match.Groups[1].Value.Trim();
                            DebugLog.Log(Tag, $"Using project from gcloud properties: {projectId}");
                            return projectId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log(Tag, $"Failed to parse gcloud properties: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Get the path to the gcloud properties file.
    /// On Windows: %APPDATA%\gcloud\properties
    /// On Unix: ~/.config/gcloud/properties
    /// </summary>
    private static string? GetGcloudPropertiesPath()
    {
        // Check CLOUDSDK_CONFIG env var first
        var configDir = Environment.GetEnvironmentVariable("CLOUDSDK_CONFIG");
        if (!string.IsNullOrEmpty(configDir))
        {
            return Path.Combine(configDir, "properties");
        }

        // On Windows, gcloud config is at %APPDATA%\gcloud\properties
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var windowsPath = Path.Combine(appData, "gcloud", "properties");
        if (File.Exists(windowsPath))
        {
            return windowsPath;
        }

        // Fallback to Unix-style path (~/.config/gcloud/properties)
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "gcloud", "properties");
    }

    /// <summary>
    /// Fetch quota usage from Google Cloud Monitoring timeSeries API.
    /// Uses the generate_content_requests_per_minute_per_project_per_base_model/usage metric
    /// and parses the response with JsonDocument to handle the complex monitoring API structure.
    /// Returns (usedPercent, errorMessage).
    /// </summary>
    private static async Task<(double usedPercent, string? error)> FetchQuotaUsageAsync(string accessToken, string projectId)
    {
        var now = DateTime.UtcNow;
        var startTime = now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var endTime = now.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var filter = Uri.EscapeDataString(
            "metric.type=\"aiplatform.googleapis.com/quota/generate_content_requests_per_minute_per_project_per_base_model/usage\""
        );

        var url = $"https://monitoring.googleapis.com/v3/projects/{projectId}/timeSeries" +
                  $"?filter={filter}" +
                  $"&interval.startTime={startTime}" +
                  $"&interval.endTime={endTime}";

        DebugLog.Log(Tag, $"Fetching: {url}");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await HttpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        DebugLog.Log(Tag, $"Response status: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            DebugLog.Log(Tag, $"Response body: {json}");
            return (0, $"Monitoring API returned {response.StatusCode}");
        }

        // Parse with JsonDocument to handle the complex monitoring API response
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("timeSeries", out var timeSeriesArray))
        {
            DebugLog.Log(Tag, "No timeSeries in response, quota usage is 0");
            return (0, null);
        }

        double maxUsedPercent = 0;

        foreach (var ts in timeSeriesArray.EnumerateArray())
        {
            // Try to extract the quota limit from metric labels
            double quotaLimit = 0;
            if (ts.TryGetProperty("metric", out var metric) &&
                metric.TryGetProperty("labels", out var labels) &&
                labels.TryGetProperty("quota_limit", out var limitElement))
            {
                if (limitElement.ValueKind == JsonValueKind.String)
                    double.TryParse(limitElement.GetString(), out quotaLimit);
                else if (limitElement.ValueKind == JsonValueKind.Number)
                    quotaLimit = limitElement.GetDouble();
            }

            if (!ts.TryGetProperty("points", out var points)) continue;

            foreach (var point in points.EnumerateArray())
            {
                if (!point.TryGetProperty("value", out var value)) continue;

                double usage = 0;

                if (value.TryGetProperty("int64Value", out var int64Val))
                {
                    if (int64Val.ValueKind == JsonValueKind.String)
                        double.TryParse(int64Val.GetString(), out usage);
                    else if (int64Val.ValueKind == JsonValueKind.Number)
                        usage = int64Val.GetDouble();
                }
                else if (value.TryGetProperty("doubleValue", out var doubleVal))
                {
                    usage = doubleVal.GetDouble();
                }

                // Calculate percentage if we have a quota limit
                double percent;
                if (quotaLimit > 0)
                {
                    percent = (usage / quotaLimit) * 100;
                }
                else
                {
                    // If no limit found, treat the value as a ratio (0-1) or raw percentage
                    percent = usage <= 1.0 && usage > 0 ? usage * 100 : usage;
                }

                if (percent > maxUsedPercent)
                    maxUsedPercent = percent;
            }
        }

        DebugLog.Log(Tag, $"Calculated quota usage: {maxUsedPercent:F1}%");
        return (maxUsedPercent, null);
    }

    /// <summary>
    /// Run a gcloud CLI command and return stdout, or null on failure
    /// </summary>
    private static async Task<string?> RunGcloudCommandAsync(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "gcloud",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Timeout after 10 seconds
            var completed = process.WaitForExit(10_000);
            if (!completed)
            {
                try { process.Kill(); } catch { }
                DebugLog.Log(Tag, "gcloud command timed out");
                return null;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                DebugLog.Log(Tag, $"gcloud exited with code {process.ExitCode}: {error}");
                return null;
            }

            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // gcloud is not installed or not in PATH
            DebugLog.Log(Tag, "gcloud CLI not found in PATH");
            return null;
        }
        catch (Exception ex)
        {
            DebugLog.Log(Tag, $"Failed to run gcloud: {ex.Message}");
            return null;
        }
    }
}

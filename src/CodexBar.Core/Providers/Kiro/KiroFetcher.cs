using System.Diagnostics;
using System.Text.Json;
using CodexBar.Core.Logging;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Kiro;

/// <summary>
/// Fetches Kiro usage data from local usage file or CLI command.
/// This is experimental - the exact JSON format may change.
/// </summary>
public class KiroFetcher : IProviderFetcher
{
    private static readonly string UsageFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".kiro",
        "usage.json"
    );

    public UsageProvider Provider => UsageProvider.Kiro;

    public Task<bool> IsAvailableAsync()
    {
        // Available if the usage file exists or kiro CLI is on PATH
        if (File.Exists(UsageFilePath))
            return Task.FromResult(true);

        var onPath = IsKiroOnPath();
        return Task.FromResult(onPath);
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            string? json = null;

            // Strategy 1: Read from local usage file
            if (File.Exists(UsageFilePath))
            {
                DebugLog.Log("Kiro", $"Reading usage from file: {UsageFilePath}");
                json = await File.ReadAllTextAsync(UsageFilePath);
            }

            // Strategy 2: Fall back to CLI command
            if (string.IsNullOrWhiteSpace(json))
            {
                DebugLog.Log("Kiro", "Usage file not found, trying kiro CLI");
                json = await RunKiroCliAsync();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Kiro,
                    Error = "No Kiro usage data found. Ensure ~/.kiro/usage.json exists or kiro CLI is on PATH."
                };
            }

            DebugLog.Log("Kiro", $"Parsing JSON ({json.Length} bytes)");
            return ParseUsageJson(json);
        }
        catch (Exception ex)
        {
            DebugLog.Log("Kiro", $"Error fetching usage: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Kiro,
                Error = ex.Message
            };
        }
    }

    private UsageSnapshot ParseUsageJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var creditsUsed = GetDoubleProperty(root, "credits_used");
            var creditsLimit = GetDoubleProperty(root, "credits_limit");
            var bonusUsed = GetDoubleProperty(root, "bonus_used");
            var bonusLimit = GetDoubleProperty(root, "bonus_limit");

            DebugLog.Log("Kiro", $"Credits: {creditsUsed}/{creditsLimit}, Bonus: {bonusUsed}/{bonusLimit}");

            if (creditsLimit <= 0)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Kiro,
                    Error = "Invalid usage data: credits_limit is zero or missing"
                };
            }

            var creditsPercent = (creditsUsed / creditsLimit) * 100;

            // Parse optional reset time
            DateTime? resetsAt = null;
            if (root.TryGetProperty("resets_at", out var resetsAtProp))
            {
                if (resetsAtProp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(resetsAtProp.GetString(), out var parsed))
                {
                    resetsAt = parsed.ToUniversalTime();
                }
            }

            // Build primary bar: Credits
            var primary = new RateWindow
            {
                Label = "Credits",
                UsedPercent = Math.Min(creditsPercent, 100),
                ResetsAt = resetsAt
            };

            // Build secondary bar: Bonus (only if bonus credits exist)
            RateWindow? secondary = null;
            if (bonusLimit > 0)
            {
                var bonusPercent = (bonusUsed / bonusLimit) * 100;
                secondary = new RateWindow
                {
                    Label = "Bonus",
                    UsedPercent = Math.Min(bonusPercent, 100),
                    ResetsAt = resetsAt
                };
            }

            // Parse optional identity info
            string? plan = null;
            if (root.TryGetProperty("plan", out var planProp) && planProp.ValueKind == JsonValueKind.String)
            {
                plan = planProp.GetString();
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.Kiro,
                Primary = primary,
                Secondary = secondary,
                Identity = new ProviderIdentity
                {
                    Plan = plan ?? "Kiro"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (JsonException ex)
        {
            DebugLog.Log("Kiro", $"JSON parse error: {ex.Message}");
            return new UsageSnapshot
            {
                Provider = UsageProvider.Kiro,
                Error = $"Failed to parse Kiro usage JSON: {ex.Message}"
            };
        }
    }

    private static double GetDoubleProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetDouble();

            // Handle string-encoded numbers
            if (prop.ValueKind == JsonValueKind.String &&
                double.TryParse(prop.GetString(), out var parsed))
                return parsed;
        }
        return 0;
    }

    private static bool IsKiroOnPath()
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var exeName = OperatingSystem.IsWindows() ? "kiro.exe" : "kiro";

            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(fullPath))
                    return true;
            }
        }
        catch
        {
            // Ignore PATH search errors
        }

        return false;
    }

    private static async Task<string?> RunKiroCliAsync()
    {
        try
        {
            var exeName = OperatingSystem.IsWindows() ? "kiro.exe" : "kiro";

            var startInfo = new ProcessStartInfo
            {
                FileName = exeName,
                Arguments = "usage --json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                DebugLog.Log("Kiro", "Failed to start kiro process");
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                DebugLog.Log("Kiro", $"CLI exited with code {process.ExitCode}: {stderr}");
                return null;
            }

            DebugLog.Log("Kiro", $"CLI returned {output.Length} bytes");
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch (Exception ex)
        {
            DebugLog.Log("Kiro", $"CLI error: {ex.Message}");
            return null;
        }
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.JetBrains;

/// <summary>
/// Fetches JetBrains AI usage from local IDE configuration files
/// </summary>
public class JetBrainsFetcher : IProviderFetcher
{
    public UsageProvider Provider => UsageProvider.JetBrains;

    public Task<bool> IsAvailableAsync()
    {
        var configPath = FindConfigPath();
        return Task.FromResult(configPath != null);
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var configPath = FindConfigPath();
            if (configPath == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.JetBrains,
                    Error = "No JetBrains IDE config found"
                };
            }

            var quota = await ParseQuotaFromXmlAsync(configPath);
            if (quota?.QuotaInfo == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.JetBrains,
                    Error = "Could not parse quota from config"
                };
            }

            return new UsageSnapshot
            {
                Provider = UsageProvider.JetBrains,
                Primary = new RateWindow
                {
                    UsedPercent = quota.QuotaInfo.UsedPercent,
                    ResetsAt = quota.RefillInfo?.NextParsed ?? quota.QuotaInfo.UntilParsed
                },
                Identity = new ProviderIdentity
                {
                    Plan = quota.QuotaInfo.Type ?? "JetBrains AI"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.JetBrains,
                Error = ex.Message
            };
        }
    }

    private static string? FindConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // JetBrains IDEs on Windows store config in %APPDATA%\JetBrains\<IDE><version>\options
        var jetbrainsDir = Path.Combine(appData, "JetBrains");
        if (!Directory.Exists(jetbrainsDir)) return null;

        // IDE directory patterns (sorted by preference)
        string[] idePatterns =
        [
            "IntelliJIdea*",
            "Rider*",
            "PyCharm*",
            "WebStorm*",
            "GoLand*",
            "CLion*",
            "PhpStorm*",
            "RubyMine*",
            "DataGrip*",
            "AndroidStudio*"
        ];

        foreach (var pattern in idePatterns)
        {
            var ideDirs = Directory.GetDirectories(jetbrainsDir, pattern)
                .OrderByDescending(d => d) // Latest version first
                .ToArray();

            foreach (var ideDir in ideDirs)
            {
                var configPath = Path.Combine(ideDir, "options", "aiAssistant.xml");
                if (File.Exists(configPath))
                {
                    return configPath;
                }
            }
        }

        return null;
    }

    private static async Task<JetBrainsQuotaResponse?> ParseQuotaFromXmlAsync(string path)
    {
        var xml = await File.ReadAllTextAsync(path);
        var doc = XDocument.Parse(xml);

        // Find the AIAssistantQuotaManager2 component
        var quotaOption = doc.Descendants("component")
            .Where(c => c.Attribute("name")?.Value == "AIAssistantQuotaManager2")
            .SelectMany(c => c.Descendants("option"))
            .FirstOrDefault(o => o.Attribute("name")?.Value == "quotaInfo");

        var jsonValue = quotaOption?.Attribute("value")?.Value;
        if (string.IsNullOrEmpty(jsonValue)) return null;

        // Decode HTML entities in the JSON string
        jsonValue = DecodeHtmlEntities(jsonValue);

        try
        {
            return JsonSerializer.Deserialize<JetBrainsQuotaResponse>(jsonValue);
        }
        catch
        {
            return null;
        }
    }

    private static string DecodeHtmlEntities(string text)
    {
        return text
            .Replace("&#10;", "\n")
            .Replace("&quot;", "\"")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">");
    }
}

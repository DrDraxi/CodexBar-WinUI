using CodexBar.Core.Models;
using CodexBar.Core.Providers.Augment;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Providers.Copilot;
using CodexBar.Core.Providers.Cursor;
using CodexBar.Core.Providers.Gemini;
using CodexBar.Core.Providers.JetBrains;

namespace CodexBar.Core.Providers;

/// <summary>
/// Registry of all available provider fetchers
/// </summary>
public static class ProviderRegistry
{
    private static readonly Dictionary<UsageProvider, IProviderFetcher> Fetchers = new()
    {
        [UsageProvider.Codex] = new CodexProvider(),
        [UsageProvider.Claude] = new ClaudeProvider(),
        [UsageProvider.Copilot] = new CopilotFetcher(),
        [UsageProvider.Cursor] = new CursorFetcher(),
        [UsageProvider.Gemini] = new GeminiFetcher(),
        [UsageProvider.JetBrains] = new JetBrainsFetcher(),
        [UsageProvider.Augment] = new AugmentFetcher(),
    };

    /// <summary>
    /// Get all registered providers
    /// </summary>
    public static IEnumerable<UsageProvider> AllProviders => Fetchers.Keys;

    /// <summary>
    /// Get fetcher for a specific provider
    /// </summary>
    public static IProviderFetcher? GetFetcher(UsageProvider provider)
    {
        return Fetchers.GetValueOrDefault(provider);
    }

    /// <summary>
    /// Fetch usage for a specific provider
    /// </summary>
    public static async Task<UsageSnapshot> FetchAsync(UsageProvider provider)
    {
        var fetcher = GetFetcher(provider);
        if (fetcher == null)
        {
            return new UsageSnapshot
            {
                Provider = provider,
                Error = "Provider not implemented"
            };
        }

        return await fetcher.FetchAsync();
    }

    /// <summary>
    /// Fetch usage for all providers in parallel
    /// </summary>
    public static async Task<Dictionary<UsageProvider, UsageSnapshot>> FetchAllAsync(IEnumerable<UsageProvider>? enabledProviders = null)
    {
        var providers = enabledProviders ?? AllProviders;
        var tasks = providers.Select(async p => (Provider: p, Snapshot: await FetchAsync(p)));
        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.Provider, r => r.Snapshot);
    }

    /// <summary>
    /// Get provider display info
    /// </summary>
    public static ProviderInfo GetProviderInfo(UsageProvider provider)
    {
        return provider switch
        {
            UsageProvider.Codex => new ProviderInfo("Codex", "#10A37F", "OAuth or browser cookies"),
            UsageProvider.Claude => new ProviderInfo("Claude", "#D97706", "OAuth or browser cookies"),
            UsageProvider.Copilot => new ProviderInfo("Copilot", "#6E40C9", "GitHub device flow"),
            UsageProvider.Cursor => new ProviderInfo("Cursor", "#00A67E", "Browser cookies"),
            UsageProvider.Gemini => new ProviderInfo("Gemini", "#4285F4", "Gemini CLI OAuth"),
            UsageProvider.JetBrains => new ProviderInfo("JetBrains", "#FE315D", "Local IDE config"),
            UsageProvider.Augment => new ProviderInfo("Augment", "#7C3AED", "Browser cookies"),
            _ => new ProviderInfo(provider.ToString(), "#666666", "Unknown")
        };
    }
}

/// <summary>
/// Provider display information
/// </summary>
public record ProviderInfo(string Name, string Color, string AuthMethod);

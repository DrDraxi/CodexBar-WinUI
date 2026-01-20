using CodexBar.Core.Models;

namespace CodexBar.Core.Providers;

/// <summary>
/// Interface for fetching usage data from a provider
/// </summary>
public interface IProviderFetcher
{
    /// <summary>
    /// The provider this fetcher handles
    /// </summary>
    UsageProvider Provider { get; }

    /// <summary>
    /// Check if this fetcher can currently fetch data (credentials available, etc.)
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Fetch current usage data
    /// </summary>
    Task<UsageSnapshot> FetchAsync();
}

/// <summary>
/// Kind of authentication/fetch method
/// </summary>
public enum ProviderFetchKind
{
    OAuth,
    Web,
    Cli,
    Api
}

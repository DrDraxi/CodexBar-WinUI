using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Codex;

/// <summary>
/// Combined Codex provider that tries multiple fetch strategies
/// </summary>
public class CodexProvider : IProviderFetcher
{
    private readonly CodexOAuthFetcher _oauthFetcher = new();
    private readonly CodexWebFetcher _webFetcher = new();

    public UsageProvider Provider => UsageProvider.Codex;

    public async Task<bool> IsAvailableAsync()
    {
        return await _oauthFetcher.IsAvailableAsync() || await _webFetcher.IsAvailableAsync();
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        // Try OAuth first (preferred)
        if (await _oauthFetcher.IsAvailableAsync())
        {
            var result = await _oauthFetcher.FetchAsync();
            if (result.IsValid)
            {
                return result;
            }
        }

        // Fall back to web cookies
        if (await _webFetcher.IsAvailableAsync())
        {
            var result = await _webFetcher.FetchAsync();
            if (result.IsValid)
            {
                return result;
            }
            return result; // Return web error if it failed
        }

        // No auth method available
        return new UsageSnapshot
        {
            Provider = UsageProvider.Codex,
            Error = "No authentication available. Install Codex CLI or sign in to chatgpt.com in Chrome/Edge."
        };
    }
}

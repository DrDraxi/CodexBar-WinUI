using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Combined Claude provider that tries multiple fetch strategies
/// </summary>
public class ClaudeProvider : IProviderFetcher
{
    private readonly ClaudeFetcher _oauthFetcher = new();
    private readonly ClaudeWebFetcher _webFetcher = new();

    public UsageProvider Provider => UsageProvider.Claude;

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
            Provider = UsageProvider.Claude,
            Error = "Claude web API blocked by Cloudflare. Use 'claude login' in terminal or paste cookies manually in Settings."
        };
    }
}

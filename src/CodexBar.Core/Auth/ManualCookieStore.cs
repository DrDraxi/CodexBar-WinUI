namespace CodexBar.Core.Auth;

/// <summary>
/// Store for manually pasted cookies per provider
/// </summary>
public static class ManualCookieStore
{
    private static readonly Dictionary<string, string> _cookies = new();

    /// <summary>
    /// Set manual cookies from settings
    /// </summary>
    public static void SetCookies(Dictionary<string, string> cookies)
    {
        _cookies.Clear();
        foreach (var kvp in cookies)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                _cookies[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Get manual cookie for a provider
    /// </summary>
    public static string? GetCookie(string providerName)
    {
        return _cookies.TryGetValue(providerName, out var cookie) ? cookie : null;
    }

    /// <summary>
    /// Check if a provider has manual cookies
    /// </summary>
    public static bool HasCookie(string providerName)
    {
        return _cookies.ContainsKey(providerName) && !string.IsNullOrEmpty(_cookies[providerName]);
    }
}

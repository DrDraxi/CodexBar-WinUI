using System.Text.Json;
using System.Text.Json.Serialization;
using CodexBar.Core.Logging;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Claude OAuth credentials stored by the Claude CLI
/// </summary>
public record ClaudeCredentials
{
    [JsonPropertyName("claudeAiOauth")]
    public ClaudeOAuthData? ClaudeAiOauth { get; init; }
}

public record ClaudeOAuthData
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expiresAt")]
    public long? ExpiresAt { get; init; }

    [JsonPropertyName("scopes")]
    public List<string>? Scopes { get; init; }

    [JsonPropertyName("subscriptionType")]
    public string? SubscriptionType { get; init; }

    [JsonPropertyName("rateLimitTier")]
    public string? RateLimitTier { get; init; }

    /// <summary>
    /// Check if credentials have user:profile scope (required for usage API)
    /// </summary>
    public bool HasProfileScope => Scopes?.Contains("user:profile") ?? false;

    /// <summary>
    /// Check if token is expired
    /// </summary>
    public bool IsExpired => ExpiresAt != null && DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAt.Value) < DateTimeOffset.UtcNow;
}

/// <summary>
/// Helper to load Claude credentials from disk
/// </summary>
public static class ClaudeCredentialsLoader
{
    /// <summary>
    /// Try to load Claude OAuth credentials from known locations
    /// </summary>
    public static async Task<ClaudeOAuthData?> LoadAsync()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] credentialPaths =
        [
            Path.Combine(userProfile, ".claude", ".credentials.json"),
            Path.Combine(userProfile, ".claude", "credentials.json"),
        ];

        foreach (var path in credentialPaths)
        {
            DebugLog.Log("ClaudeCredentials", $"Checking path: {path}, exists: {File.Exists(path)}");
            if (!File.Exists(path)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(path);
                DebugLog.Log("ClaudeCredentials", $"Read {json.Length} bytes from {path}");
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var credentials = JsonSerializer.Deserialize<ClaudeCredentials>(json, options);
                DebugLog.Log("ClaudeCredentials", $"Parsed: hasOauth={credentials?.ClaudeAiOauth != null}, hasToken={credentials?.ClaudeAiOauth?.AccessToken != null}");
                if (credentials?.ClaudeAiOauth != null)
                {
                    var oauth = credentials.ClaudeAiOauth;
                    DebugLog.Log("ClaudeCredentials", $"Token (first 20): {oauth.AccessToken?.Substring(0, Math.Min(20, oauth.AccessToken?.Length ?? 0))}..., scopes: {string.Join(",", oauth.Scopes ?? new List<string>())}, subscriptionType: {oauth.SubscriptionType}");
                }
                if (credentials?.ClaudeAiOauth?.AccessToken != null)
                {
                    return credentials.ClaudeAiOauth;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log("ClaudeCredentials", $"Error parsing {path}: {ex.Message}");
                // Continue to next path
            }
        }

        return null;
    }

    /// <summary>
    /// Get the path where credentials would be loaded from (for debugging)
    /// </summary>
    public static string GetCredentialsPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(userProfile, ".claude", ".credentials.json");
        return $"{path} (exists: {File.Exists(path)})";
    }
}

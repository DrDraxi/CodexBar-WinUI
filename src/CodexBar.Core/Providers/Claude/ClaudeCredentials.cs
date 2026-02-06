using System.Net.Http;
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

internal record TokenRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }
}

/// <summary>
/// Helper to load Claude credentials from disk
/// </summary>
public static class ClaudeCredentialsLoader
{
    private static readonly HttpClient HttpClient = new();
    private const string RefreshEndpoint = "https://platform.claude.com/v1/oauth/token";
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

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
    /// Load credentials with automatic token refresh if expired
    /// </summary>
    public static async Task<ClaudeOAuthData?> LoadWithAutoRefreshAsync()
    {
        var credentials = await LoadAsync();
        if (credentials == null) return null;

        if (!credentials.IsExpired)
            return credentials;

        if (string.IsNullOrEmpty(credentials.RefreshToken))
        {
            DebugLog.Log("ClaudeCredentials", "Access token expired but no refresh token available");
            return null;
        }

        DebugLog.Log("ClaudeCredentials", "Access token expired, attempting auto-refresh");

        try
        {
            var refreshed = await RefreshTokenAsync(credentials);
            if (refreshed != null)
            {
                await SaveCredentialsAsync(refreshed);
                DebugLog.Log("ClaudeCredentials", "Token refresh successful");
                return refreshed;
            }
        }
        catch (Exception ex)
        {
            DebugLog.Log("ClaudeCredentials", $"Token refresh failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Refresh an expired OAuth token using the refresh token
    /// </summary>
    public static async Task<ClaudeOAuthData?> RefreshTokenAsync(ClaudeOAuthData credentials)
    {
        if (string.IsNullOrEmpty(credentials.RefreshToken))
            return null;

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = credentials.RefreshToken,
            ["client_id"] = ClientId
        });

        var response = await HttpClient.PostAsync(RefreshEndpoint, content);
        var json = await response.Content.ReadAsStringAsync();
        DebugLog.Log("ClaudeCredentials", $"Refresh response: {response.StatusCode}, Body (truncated): {json.Substring(0, Math.Min(200, json.Length))}");

        if (!response.IsSuccessStatusCode)
            return null;

        var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(json);
        if (tokenResponse?.AccessToken == null)
            return null;

        var expiresAt = tokenResponse.ExpiresIn != null
            ? DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value).ToUnixTimeMilliseconds()
            : credentials.ExpiresAt;

        return credentials with
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken ?? credentials.RefreshToken,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Save refreshed credentials back to the credentials file
    /// </summary>
    private static async Task SaveCredentialsAsync(ClaudeOAuthData credentials)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(userProfile, ".claude", ".credentials.json");

        var wrapper = new ClaudeCredentials { ClaudeAiOauth = credentials };
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(wrapper, options);
        await File.WriteAllTextAsync(path, json);
        DebugLog.Log("ClaudeCredentials", $"Saved refreshed credentials to {path}");
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

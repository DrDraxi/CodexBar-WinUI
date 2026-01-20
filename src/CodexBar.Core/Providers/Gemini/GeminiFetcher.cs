using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Gemini;

/// <summary>
/// Fetches Gemini usage data using CLI OAuth credentials
/// </summary>
public class GeminiFetcher : IProviderFetcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private const string QuotaUrl = "https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota";
    private const string TokenRefreshUrl = "https://oauth2.googleapis.com/token";

    private const string ClientId = "";
    private const string ClientSecret = "";

    public UsageProvider Provider => UsageProvider.Gemini;

    public async Task<bool> IsAvailableAsync()
    {
        var credentials = await LoadCredentialsAsync();
        return credentials?.IsOAuthPersonal == true;
    }

    public async Task<UsageSnapshot> FetchAsync()
    {
        try
        {
            var credentials = await LoadCredentialsAsync();
            if (credentials == null)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Gemini,
                    Error = "No Gemini CLI credentials found at ~/.gemini/oauth_creds.json"
                };
            }

            if (!credentials.IsOAuthPersonal)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Gemini,
                    Error = $"Unsupported auth type: {credentials.AuthType}. Only oauth-personal is supported."
                };
            }

            // Refresh token if expired
            var accessToken = credentials.AccessToken;
            if (credentials.IsExpired && !string.IsNullOrEmpty(credentials.RefreshToken))
            {
                var refreshed = await RefreshTokenAsync(credentials.RefreshToken);
                if (refreshed?.AccessToken != null)
                {
                    accessToken = refreshed.AccessToken;
                }
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Gemini,
                    Error = "No valid access token"
                };
            }

            var quota = await FetchQuotaAsync(accessToken);
            if (quota?.Buckets == null || quota.Buckets.Count == 0)
            {
                return new UsageSnapshot
                {
                    Provider = UsageProvider.Gemini,
                    Error = "Failed to fetch quota data"
                };
            }

            // Find Flash (primary) and Pro (secondary) quotas
            var flashBucket = quota.Buckets.FirstOrDefault(b => b.ModelId?.Contains("flash") == true);
            var proBucket = quota.Buckets.FirstOrDefault(b => b.ModelId?.Contains("pro") == true);

            // Use lowest remaining for each model type
            var primaryBucket = flashBucket ?? quota.Buckets.FirstOrDefault();

            return new UsageSnapshot
            {
                Provider = UsageProvider.Gemini,
                Primary = primaryBucket != null ? new RateWindow
                {
                    UsedPercent = primaryBucket.PercentUsed,
                    ResetsAt = primaryBucket.ResetTimeParsed
                } : null,
                Secondary = proBucket != null ? new RateWindow
                {
                    UsedPercent = proBucket.PercentUsed,
                    ResetsAt = proBucket.ResetTimeParsed
                } : null,
                Identity = new ProviderIdentity
                {
                    Plan = "Gemini"
                },
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new UsageSnapshot
            {
                Provider = UsageProvider.Gemini,
                Error = ex.Message
            };
        }
    }

    private static async Task<GeminiCredentials?> LoadCredentialsAsync()
    {
        var credentialsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gemini",
            "oauth_creds.json"
        );

        if (!File.Exists(credentialsPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(credentialsPath);
            return JsonSerializer.Deserialize<GeminiCredentials>(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<TokenRefreshResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            });

            var response = await HttpClient.PostAsync(TokenRefreshUrl, content);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TokenRefreshResponse>(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<GeminiQuotaResponse?> FetchQuotaAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, QuotaUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<GeminiQuotaResponse>(json);
    }
}

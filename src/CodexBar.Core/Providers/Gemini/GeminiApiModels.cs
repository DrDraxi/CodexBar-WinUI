using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Gemini;

/// <summary>
/// Gemini OAuth credentials from CLI
/// </summary>
public record GeminiCredentials
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expiry_date")]
    public long? ExpiryDate { get; init; } // milliseconds since epoch

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    [JsonPropertyName("auth_type")]
    public string? AuthType { get; init; } // "oauth-personal", "api-key", "vertex-ai"

    public bool IsExpired => ExpiryDate.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > ExpiryDate.Value;
    public bool IsOAuthPersonal => AuthType == "oauth-personal";
}

/// <summary>
/// Gemini quota response from API
/// POST https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota
/// </summary>
public record GeminiQuotaResponse
{
    [JsonPropertyName("buckets")]
    public List<GeminiQuotaBucket>? Buckets { get; init; }
}

public record GeminiQuotaBucket
{
    [JsonPropertyName("remainingFraction")]
    public double? RemainingFraction { get; init; } // 0-1

    [JsonPropertyName("resetTime")]
    public string? ResetTime { get; init; } // ISO8601

    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; } // "gemini-2.0-flash", "gemini-2.0-pro"

    [JsonPropertyName("tokenType")]
    public string? TokenType { get; init; }

    public double PercentUsed => RemainingFraction.HasValue ? (1 - RemainingFraction.Value) * 100 : 0;
    public double PercentRemaining => (RemainingFraction ?? 0) * 100;

    public DateTime? ResetTimeParsed
    {
        get
        {
            if (string.IsNullOrEmpty(ResetTime)) return null;
            if (DateTime.TryParse(ResetTime, out var dt)) return dt.ToUniversalTime();
            return null;
        }
    }
}

/// <summary>
/// Token refresh response
/// </summary>
public record TokenRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }
}

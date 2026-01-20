using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Copilot;

/// <summary>
/// GitHub Device Flow - Initial code request response
/// </summary>
public record DeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string? DeviceCode { get; init; }

    [JsonPropertyName("user_code")]
    public string? UserCode { get; init; }

    [JsonPropertyName("verification_uri")]
    public string? VerificationUri { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public int Interval { get; init; } = 5;
}

/// <summary>
/// GitHub Device Flow - Token response
/// </summary>
public record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

/// <summary>
/// Copilot usage response from internal API
/// </summary>
public record CopilotUsageResponse
{
    [JsonPropertyName("quota_snapshots")]
    public CopilotQuotaSnapshots? QuotaSnapshots { get; init; }

    [JsonPropertyName("copilot_plan")]
    public string? CopilotPlan { get; init; }

    [JsonPropertyName("assigned_date")]
    public string? AssignedDate { get; init; }

    [JsonPropertyName("quota_reset_date")]
    public string? QuotaResetDate { get; init; }
}

public record CopilotQuotaSnapshots
{
    [JsonPropertyName("premium_interactions")]
    public CopilotQuotaSnapshot? PremiumInteractions { get; init; }

    [JsonPropertyName("chat")]
    public CopilotQuotaSnapshot? Chat { get; init; }
}

public record CopilotQuotaSnapshot
{
    [JsonPropertyName("entitlement")]
    public double Entitlement { get; init; }

    [JsonPropertyName("remaining")]
    public double Remaining { get; init; }

    [JsonPropertyName("percent_remaining")]
    public double PercentRemaining { get; init; }

    [JsonPropertyName("quota_id")]
    public string? QuotaId { get; init; }

    /// <summary>
    /// Calculate percent used (100 - remaining)
    /// </summary>
    public double PercentUsed => 100 - PercentRemaining;
}

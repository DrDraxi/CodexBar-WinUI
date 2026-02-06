using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Kimi;

/// <summary>
/// Kimi usage API response
/// POST https://kimi.com/api/user/usage
/// Expected: { "weekly_used": 100, "weekly_limit": 1024, "rate_used": 5, "rate_limit": 50 }
/// </summary>
public record KimiUsageResponse
{
    [JsonPropertyName("weekly_used")]
    public int? WeeklyUsed { get; init; }

    [JsonPropertyName("weekly_limit")]
    public int? WeeklyLimit { get; init; }

    [JsonPropertyName("rate_used")]
    public int? RateUsed { get; init; }

    [JsonPropertyName("rate_limit")]
    public int? RateLimit { get; init; }

    /// <summary>
    /// Calculated weekly usage percentage (0-100)
    /// </summary>
    public double WeeklyPercentUsed =>
        WeeklyLimit > 0 ? (double)(WeeklyUsed ?? 0) / WeeklyLimit.Value * 100 : 0;

    /// <summary>
    /// Calculated 5-hour rate limit usage percentage (0-100)
    /// </summary>
    public double RatePercentUsed =>
        RateLimit > 0 ? (double)(RateUsed ?? 0) / RateLimit.Value * 100 : 0;

    /// <summary>
    /// Whether the response contains rate limit data
    /// </summary>
    public bool HasRateLimit => RateLimit > 0;
}

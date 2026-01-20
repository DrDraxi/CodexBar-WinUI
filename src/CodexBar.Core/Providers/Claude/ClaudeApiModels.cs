using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Response from Claude OAuth usage API
/// GET https://api.anthropic.com/api/oauth/usage
/// </summary>
public record ClaudeUsageResponse
{
    [JsonPropertyName("five_hour")]
    public ClaudeUsageWindow? FiveHour { get; init; }

    [JsonPropertyName("seven_day")]
    public ClaudeUsageWindow? SevenDay { get; init; }

    [JsonPropertyName("seven_day_sonnet")]
    public ClaudeUsageWindow? SevenDaySonnet { get; init; }

    [JsonPropertyName("seven_day_opus")]
    public ClaudeUsageWindow? SevenDayOpus { get; init; }

    [JsonPropertyName("extra_usage")]
    public ClaudeExtraUsage? ExtraUsage { get; init; }
}

public record ClaudeUsageWindow
{
    [JsonPropertyName("percent_used")]
    public double? PercentUsed { get; init; }

    /// <summary>
    /// Alternative field name used by OAuth API
    /// </summary>
    [JsonPropertyName("utilization")]
    public double? Utilization { get; init; }

    /// <summary>
    /// Get the usage percentage from either field
    /// </summary>
    public double? UsagePercent => PercentUsed ?? Utilization;

    [JsonPropertyName("resets_at")]
    public string? ResetsAt { get; init; }

    /// <summary>
    /// Parse reset time to DateTime
    /// </summary>
    public DateTime? ResetsAtParsed
    {
        get
        {
            if (string.IsNullOrEmpty(ResetsAt)) return null;
            if (DateTime.TryParse(ResetsAt, out var dt)) return dt.ToUniversalTime();
            return null;
        }
    }
}

public record ClaudeExtraUsage
{
    [JsonPropertyName("spend")]
    public double? Spend { get; init; }

    [JsonPropertyName("limit")]
    public double? Limit { get; init; }
}

/// <summary>
/// Response from Claude web API for organizations
/// GET https://claude.ai/api/organizations
/// </summary>
public record ClaudeOrganization
{
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// Response from Claude web API for organization usage
/// GET https://claude.ai/api/organizations/{orgId}/usage
/// </summary>
public record ClaudeWebUsageResponse
{
    [JsonPropertyName("session_used_percent")]
    public double? SessionUsedPercent { get; init; }

    [JsonPropertyName("session_window_remaining")]
    public int? SessionWindowRemaining { get; init; }

    [JsonPropertyName("weekly_used_percent")]
    public double? WeeklyUsedPercent { get; init; }

    [JsonPropertyName("weekly_window_remaining")]
    public int? WeeklyWindowRemaining { get; init; }

    [JsonPropertyName("opus_used_percent")]
    public double? OpusUsedPercent { get; init; }
}

/// <summary>
/// Response from Claude web API for account info
/// GET https://claude.ai/api/account
/// </summary>
public record ClaudeAccountResponse
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("memberships")]
    public List<ClaudeMembership>? Memberships { get; init; }
}

public record ClaudeMembership
{
    [JsonPropertyName("organization")]
    public ClaudeOrganization? Organization { get; init; }
}

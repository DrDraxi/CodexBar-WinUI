using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.OpenCode;

/// <summary>
/// OpenCode usage response
/// POST https://opencode.ai/_server { "fn": "getUsage" }
/// </summary>
public record OpenCodeUsageResponse
{
    [JsonPropertyName("five_hour_percent")]
    public double? FiveHourPercent { get; init; }

    [JsonPropertyName("weekly_percent")]
    public double? WeeklyPercent { get; init; }

    [JsonPropertyName("plan")]
    public string? Plan { get; init; }
}

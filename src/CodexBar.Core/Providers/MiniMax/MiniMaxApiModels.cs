using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.MiniMax;

public record MiniMaxCodingPlanResponse
{
    [JsonPropertyName("used")]
    public double? Used { get; init; }

    [JsonPropertyName("limit")]
    public double? Limit { get; init; }

    [JsonPropertyName("plan_name")]
    public string? PlanName { get; init; }

    public double PercentUsed => (Limit > 0 && Used.HasValue) ? (Used.Value / Limit.Value) * 100 : 0;
}

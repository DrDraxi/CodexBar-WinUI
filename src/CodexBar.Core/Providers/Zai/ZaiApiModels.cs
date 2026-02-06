using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Zai;

public record ZaiUsageResponse
{
    [JsonPropertyName("tokens_used")]
    public double? TokensUsed { get; init; }

    [JsonPropertyName("tokens_limit")]
    public double? TokensLimit { get; init; }

    [JsonPropertyName("mcp_used")]
    public double? McpUsed { get; init; }

    [JsonPropertyName("mcp_limit")]
    public double? McpLimit { get; init; }

    public double TokensPercent =>
        (TokensLimit > 0 && TokensUsed.HasValue) ? (TokensUsed.Value / TokensLimit.Value) * 100 : 0;

    public double McpPercent =>
        (McpLimit > 0 && McpUsed.HasValue) ? (McpUsed.Value / McpLimit.Value) * 100 : 0;

    public bool HasMcp => McpUsed.HasValue && McpLimit.HasValue;
}

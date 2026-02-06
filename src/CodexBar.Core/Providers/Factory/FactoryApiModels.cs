using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Factory;

/// <summary>
/// Factory (Droid) usage API response
/// GET https://app.factory.ai/api/usage
/// </summary>
public record FactoryUsageResponse
{
    [JsonPropertyName("standard")]
    public FactoryTokenBucket? Standard { get; init; }

    [JsonPropertyName("premium")]
    public FactoryTokenBucket? Premium { get; init; }
}

public record FactoryTokenBucket
{
    [JsonPropertyName("used")]
    public long Used { get; init; }

    [JsonPropertyName("limit")]
    public long Limit { get; init; }

    public double PercentUsed => Limit > 0 ? (Used / (double)Limit) * 100 : 0;
}

using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.KimiK2;

public record KimiK2CreditsResponse
{
    [JsonPropertyName("consumed")]
    public double? Consumed { get; init; }

    [JsonPropertyName("remaining")]
    public double? Remaining { get; init; }

    public double PercentUsed
    {
        get
        {
            var total = (Consumed ?? 0) + (Remaining ?? 0);
            return total > 0 ? ((Consumed ?? 0) / total) * 100 : 0;
        }
    }
}

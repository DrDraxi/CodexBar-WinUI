using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Amp;

/// <summary>
/// Represents the freeTierUsage JSON object scraped from the ampcode.com/settings page
/// </summary>
public record AmpFreeTierUsage
{
    [JsonPropertyName("used")]
    public double? Used { get; init; }

    [JsonPropertyName("limit")]
    public double? Limit { get; init; }

    [JsonPropertyName("resetsAt")]
    public string? ResetsAt { get; init; }

    public double PercentUsed => (Limit > 0 && Used.HasValue) ? (Used.Value / Limit.Value) * 100 : 0;

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

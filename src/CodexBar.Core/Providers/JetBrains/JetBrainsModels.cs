using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.JetBrains;

/// <summary>
/// JetBrains AI quota info from local XML config
/// </summary>
public record JetBrainsQuotaInfo
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("used")]
    public double Used { get; init; }

    [JsonPropertyName("maximum")]
    public double Maximum { get; init; }

    [JsonPropertyName("available")]
    public double Available { get; init; }

    [JsonPropertyName("until")]
    public string? Until { get; init; }

    public double UsedPercent => Maximum > 0 ? (Used / Maximum) * 100 : 0;
    public double RemainingPercent => Maximum > 0 ? (Available / Maximum) * 100 : 0;

    public DateTime? UntilParsed
    {
        get
        {
            if (string.IsNullOrEmpty(Until)) return null;
            if (DateTime.TryParse(Until, out var dt)) return dt.ToUniversalTime();
            return null;
        }
    }
}

/// <summary>
/// JetBrains AI tariff quota from XML
/// </summary>
public record JetBrainsTariffQuota
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("available")]
    public double Available { get; init; }
}

/// <summary>
/// JetBrains AI refill info
/// </summary>
public record JetBrainsRefillInfo
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("next")]
    public string? Next { get; init; }

    [JsonPropertyName("amount")]
    public double? Amount { get; init; }

    public DateTime? NextParsed
    {
        get
        {
            if (string.IsNullOrEmpty(Next)) return null;
            if (DateTime.TryParse(Next, out var dt)) return dt.ToUniversalTime();
            return null;
        }
    }
}

/// <summary>
/// Root quota response embedded in XML
/// </summary>
public record JetBrainsQuotaResponse
{
    [JsonPropertyName("quotaInfo")]
    public JetBrainsQuotaInfo? QuotaInfo { get; init; }

    [JsonPropertyName("tariffQuota")]
    public JetBrainsTariffQuota? TariffQuota { get; init; }

    [JsonPropertyName("refillInfo")]
    public JetBrainsRefillInfo? RefillInfo { get; init; }
}

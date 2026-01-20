using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Augment;

/// <summary>
/// Augment credits response
/// GET https://app.augmentcode.com/api/credits
/// </summary>
public record AugmentCreditsResponse
{
    [JsonPropertyName("usageUnitsRemaining")]
    public double? UsageUnitsRemaining { get; init; }

    [JsonPropertyName("usageUnitsConsumedThisBillingCycle")]
    public double? UsageUnitsConsumedThisBillingCycle { get; init; }

    [JsonPropertyName("usageUnitsAvailable")]
    public double? UsageUnitsAvailable { get; init; }

    [JsonPropertyName("usageBalanceStatus")]
    public string? UsageBalanceStatus { get; init; }

    public double PercentUsed
    {
        get
        {
            if (UsageUnitsAvailable > 0 && UsageUnitsConsumedThisBillingCycle.HasValue)
            {
                return (UsageUnitsConsumedThisBillingCycle.Value / UsageUnitsAvailable.Value) * 100;
            }
            if (UsageUnitsAvailable > 0 && UsageUnitsRemaining.HasValue)
            {
                return ((UsageUnitsAvailable.Value - UsageUnitsRemaining.Value) / UsageUnitsAvailable.Value) * 100;
            }
            return 0;
        }
    }
}

/// <summary>
/// Augment subscription response
/// GET https://app.augmentcode.com/api/subscription
/// </summary>
public record AugmentSubscriptionResponse
{
    [JsonPropertyName("planName")]
    public string? PlanName { get; init; }

    [JsonPropertyName("billingPeriodEnd")]
    public string? BillingPeriodEnd { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    public DateTime? BillingPeriodEndParsed
    {
        get
        {
            if (string.IsNullOrEmpty(BillingPeriodEnd)) return null;
            if (DateTime.TryParse(BillingPeriodEnd, out var dt)) return dt.ToUniversalTime();
            return null;
        }
    }
}

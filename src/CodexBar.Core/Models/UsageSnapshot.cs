namespace CodexBar.Core.Models;

/// <summary>
/// Snapshot of usage data for a provider at a point in time
/// </summary>
public record UsageSnapshot
{
    /// <summary>
    /// The provider this snapshot is for
    /// </summary>
    public UsageProvider Provider { get; init; }

    /// <summary>
    /// Primary rate window (typically 5-hour session)
    /// </summary>
    public RateWindow? Primary { get; init; }

    /// <summary>
    /// Secondary rate window (typically weekly)
    /// </summary>
    public RateWindow? Secondary { get; init; }

    /// <summary>
    /// Tertiary rate window (model-specific, e.g., Opus limit)
    /// </summary>
    public RateWindow? Tertiary { get; init; }

    /// <summary>
    /// Pay-per-use / on-demand cost tracking (separate from rate limits)
    /// </summary>
    public ProviderCostSnapshot? ProviderCost { get; init; }

    /// <summary>
    /// When this snapshot was captured
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User identity information (email, plan, etc.)
    /// </summary>
    public ProviderIdentity? Identity { get; init; }

    /// <summary>
    /// Error message if fetch failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Whether this snapshot contains valid data
    /// </summary>
    public bool IsValid => Error == null && (Primary != null || Secondary != null || ProviderCost != null);
}

/// <summary>
/// User identity information from a provider
/// </summary>
public record ProviderIdentity
{
    public string? Email { get; init; }
    public string? Plan { get; init; }
    public string? Organization { get; init; }
}

/// <summary>
/// Pay-per-use / on-demand cost snapshot (e.g., Claude "Extra usage", Cursor "On-Demand")
/// </summary>
public record ProviderCostSnapshot
{
    /// <summary>
    /// Amount used (in currency or quota units)
    /// </summary>
    public double Used { get; init; }

    /// <summary>
    /// Limit/budget (in currency or quota units)
    /// </summary>
    public double Limit { get; init; }

    /// <summary>
    /// Currency code (e.g., "USD") or "Quota" for quota-based systems
    /// </summary>
    public string CurrencyCode { get; init; } = "USD";

    /// <summary>
    /// Period label (e.g., "Monthly", "This month")
    /// </summary>
    public string? Period { get; init; }

    /// <summary>
    /// When the period resets
    /// </summary>
    public DateTime? ResetsAt { get; init; }

    /// <summary>
    /// Percentage used
    /// </summary>
    public double PercentUsed => Limit > 0 ? (Used / Limit) * 100 : 0;
}

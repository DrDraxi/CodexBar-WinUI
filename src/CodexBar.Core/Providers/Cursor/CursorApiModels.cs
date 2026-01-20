using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Cursor;

/// <summary>
/// Cursor usage summary response
/// GET https://cursor.com/api/usage-summary
/// </summary>
public record CursorUsageSummary
{
    [JsonPropertyName("billingCycleStart")]
    public string? BillingCycleStart { get; init; }

    [JsonPropertyName("billingCycleEnd")]
    public string? BillingCycleEnd { get; init; }

    [JsonPropertyName("membershipType")]
    public string? MembershipType { get; init; } // "enterprise", "pro", "hobby"

    [JsonPropertyName("limitType")]
    public string? LimitType { get; init; }

    [JsonPropertyName("isUnlimited")]
    public bool? IsUnlimited { get; init; }

    [JsonPropertyName("individualUsage")]
    public CursorIndividualUsage? IndividualUsage { get; init; }

    [JsonPropertyName("teamUsage")]
    public CursorTeamUsage? TeamUsage { get; init; }

    public DateTime? BillingCycleEndParsed
    {
        get
        {
            if (string.IsNullOrEmpty(BillingCycleEnd)) return null;
            if (DateTime.TryParse(BillingCycleEnd, out var dt)) return dt.ToUniversalTime();
            return null;
        }
    }
}

public record CursorIndividualUsage
{
    [JsonPropertyName("plan")]
    public CursorPlanUsage? Plan { get; init; }

    [JsonPropertyName("onDemand")]
    public CursorOnDemandUsage? OnDemand { get; init; }
}

public record CursorTeamUsage
{
    [JsonPropertyName("plan")]
    public CursorPlanUsage? Plan { get; init; }
}

public record CursorPlanUsage
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("used")]
    public int? Used { get; init; } // cents

    [JsonPropertyName("limit")]
    public int? Limit { get; init; } // cents

    [JsonPropertyName("remaining")]
    public int? Remaining { get; init; }

    [JsonPropertyName("breakdown")]
    public CursorPlanBreakdown? Breakdown { get; init; }

    [JsonPropertyName("autoPercentUsed")]
    public double? AutoPercentUsed { get; init; }

    [JsonPropertyName("apiPercentUsed")]
    public double? ApiPercentUsed { get; init; }

    [JsonPropertyName("totalPercentUsed")]
    public double? TotalPercentUsed { get; init; }

    public double CalculatedPercentUsed
    {
        get
        {
            if (TotalPercentUsed.HasValue) return TotalPercentUsed.Value;
            if (Breakdown?.Total > 0 && Used.HasValue) return (Used.Value / (double)Breakdown.Total) * 100;
            if (Limit > 0 && Used.HasValue) return (Used.Value / (double)Limit.Value) * 100;
            return 0;
        }
    }
}

public record CursorPlanBreakdown
{
    [JsonPropertyName("total")]
    public int? Total { get; init; }

    [JsonPropertyName("auto")]
    public int? Auto { get; init; }

    [JsonPropertyName("api")]
    public int? Api { get; init; }
}

public record CursorOnDemandUsage
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("used")]
    public int? Used { get; init; } // cents

    [JsonPropertyName("limit")]
    public int? Limit { get; init; } // cents (if set)

    [JsonPropertyName("hardLimit")]
    public int? HardLimit { get; init; } // cents
}

/// <summary>
/// Cursor user info response
/// GET https://cursor.com/api/auth/me
/// </summary>
public record CursorUserInfo
{
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("emailVerified")]
    public bool? EmailVerified { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("sub")]
    public string? Sub { get; init; } // User ID

    [JsonPropertyName("picture")]
    public string? Picture { get; init; }
}

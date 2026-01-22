namespace CodexBar.Core.Models;

/// <summary>
/// Represents a rate-limited usage window (session, weekly, etc.)
/// </summary>
public record RateWindow
{
    /// <summary>
    /// Custom label for this window (e.g., "Auto", "Named", "Session")
    /// If null, the UI will use default labels based on window position
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Percentage of the window that has been used (0-100)
    /// </summary>
    public double UsedPercent { get; init; }

    /// <summary>
    /// Duration of the window in minutes (e.g., 300 for 5-hour session)
    /// </summary>
    public int? WindowMinutes { get; init; }

    /// <summary>
    /// When this window resets
    /// </summary>
    public DateTime? ResetsAt { get; init; }

    /// <summary>
    /// Human-readable reset time (e.g., "4h 23m")
    /// </summary>
    public string ResetTimeDisplay
    {
        get
        {
            if (ResetsAt == null) return "--";
            var remaining = ResetsAt.Value - DateTime.UtcNow;
            if (remaining.TotalMinutes < 1) return "< 1m";
            if (remaining.TotalHours < 1) return $"{(int)remaining.TotalMinutes}m";
            if (remaining.TotalDays < 1) return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        }
    }

    /// <summary>
    /// Percentage remaining (100 - UsedPercent)
    /// </summary>
    public double RemainingPercent => 100 - UsedPercent;
}

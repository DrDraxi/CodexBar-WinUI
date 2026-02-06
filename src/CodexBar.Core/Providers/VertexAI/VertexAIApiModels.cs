using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.VertexAI;

/// <summary>
/// Minimal record types for Google Cloud Monitoring timeSeries response.
/// Only models enough structure to extract quota usage percentage.
/// </summary>
public record VertexAITimeSeriesResponse
{
    [JsonPropertyName("timeSeries")]
    public List<VertexAITimeSeries>? TimeSeries { get; init; }
}

public record VertexAITimeSeries
{
    [JsonPropertyName("metric")]
    public VertexAIMetric? Metric { get; init; }

    [JsonPropertyName("points")]
    public List<VertexAIPoint>? Points { get; init; }
}

public record VertexAIMetric
{
    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; init; }
}

public record VertexAIPoint
{
    [JsonPropertyName("interval")]
    public VertexAIInterval? Interval { get; init; }

    [JsonPropertyName("value")]
    public VertexAIValue? Value { get; init; }
}

public record VertexAIInterval
{
    [JsonPropertyName("startTime")]
    public string? StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public string? EndTime { get; init; }
}

public record VertexAIValue
{
    [JsonPropertyName("int64Value")]
    public string? Int64Value { get; init; }

    [JsonPropertyName("doubleValue")]
    public double? DoubleValue { get; init; }
}

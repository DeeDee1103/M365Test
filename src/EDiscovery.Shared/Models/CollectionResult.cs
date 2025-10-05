using System.Text.Json.Serialization;

namespace EDiscovery.Shared.Models;

/// <summary>
/// Result of a collection operation from either Graph API or Graph Data Connect
/// </summary>
public class CollectionResult
{
    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("items")]
    public List<CollectedItem> Items { get; set; } = new();

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("totalItemCount")]
    public int TotalItemCount { get; set; }

    [JsonPropertyName("collectedItemsCount")]
    public int CollectedItemsCount { get; set; }

    [JsonPropertyName("collectedSizeBytes")]
    public long CollectedSizeBytes { get; set; }

    [JsonPropertyName("manifestHash")]
    public string? ManifestHash { get; set; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, object> Metrics { get; set; } = new();

    [JsonPropertyName("collectionMetadata")]
    public Dictionary<string, object>? CollectionMetadata { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue ? EndTime.Value - StartTime.Value : null;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("collectionRoute")]
    public CollectionRoute? CollectionRoute { get; set; }

    [JsonPropertyName("processingStatistics")]
    public CollectionStatistics? ProcessingStatistics { get; set; }
}

/// <summary>
/// Detailed statistics about a collection operation
/// </summary>
public class CollectionStatistics
{
    [JsonPropertyName("apiCallsCount")]
    public int ApiCallsCount { get; set; }

    [JsonPropertyName("throttleDelaysCount")]
    public int ThrottleDelaysCount { get; set; }

    [JsonPropertyName("totalThrottleTime")]
    public TimeSpan TotalThrottleTime { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("averageItemSize")]
    public double AverageItemSize => CollectedItemsCount > 0 ? (double)CollectedSizeBytes / CollectedItemsCount : 0;

    [JsonPropertyName("collectedItemsCount")]
    public int CollectedItemsCount { get; set; }

    [JsonPropertyName("collectedSizeBytes")]
    public long CollectedSizeBytes { get; set; }

    [JsonPropertyName("processingRate")]
    public double ProcessingRate { get; set; } // Items per second

    [JsonPropertyName("throughputMbps")]
    public double ThroughputMbps { get; set; } // MB per second

    [JsonPropertyName("errors")]
    public List<CollectionError> Errors { get; set; } = new();
}

/// <summary>
/// Error that occurred during collection
/// </summary>
public class CollectionError
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }
}
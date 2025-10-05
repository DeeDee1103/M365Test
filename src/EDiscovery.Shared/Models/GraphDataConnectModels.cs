using System.Text.Json.Serialization;

namespace EDiscovery.Shared.Models;

/// <summary>
/// Message sent to Azure Service Bus to trigger Azure Data Factory pipeline for Graph Data Connect collection
/// </summary>
public class GdcTriggerMessage
{
    [JsonPropertyName("pipelineRunId")]
    public string PipelineRunId { get; set; } = string.Empty;

    [JsonPropertyName("collectionRequest")]
    public CollectionRequest CollectionRequest { get; set; } = new();

    [JsonPropertyName("triggeredAt")]
    public DateTime TriggeredAt { get; set; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public GdcPriority Priority { get; set; }

    [JsonPropertyName("estimatedDuration")]
    public TimeSpan EstimatedDuration { get; set; }

    [JsonPropertyName("retryPolicy")]
    public GdcRetryPolicy RetryPolicy { get; set; } = new();

    [JsonPropertyName("configuration")]
    public GdcConfiguration Configuration { get; set; } = new();
}

/// <summary>
/// Status of a Graph Data Connect pipeline execution
/// </summary>
public class GdcPipelineStatus
{
    [JsonPropertyName("pipelineRunId")]
    public string PipelineRunId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public GdcPipelineState Status { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; } // 0-100 percentage

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("estimatedCompletion")]
    public DateTime? EstimatedCompletion { get; set; }

    [JsonPropertyName("collectedItems")]
    public int CollectedItems { get; set; }

    [JsonPropertyName("collectedSizeBytes")]
    public long CollectedSizeBytes { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("activityLog")]
    public List<GdcActivityLogEntry> ActivityLog { get; set; } = new();
}

/// <summary>
/// Activity log entry for GDC pipeline execution
/// </summary>
public class GdcActivityLogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("activity")]
    public string Activity { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Retry policy for GDC pipeline failures
/// </summary>
public class GdcRetryPolicy
{
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 3;

    [JsonPropertyName("retryDelayMinutes")]
    public int RetryDelayMinutes { get; set; } = 15;

    [JsonPropertyName("exponentialBackoff")]
    public bool ExponentialBackoff { get; set; } = true;

    [JsonPropertyName("retryableErrors")]
    public List<string> RetryableErrors { get; set; } = new()
    {
        "ThrottlingError",
        "TemporaryServiceUnavailable",
        "InternalServerError"
    };
}

/// <summary>
/// Configuration for GDC pipeline execution
/// </summary>
public class GdcConfiguration
{
    [JsonPropertyName("outputFormat")]
    public string OutputFormat { get; set; } = "Parquet";

    [JsonPropertyName("compressionType")]
    public string CompressionType { get; set; } = "Snappy";

    [JsonPropertyName("partitionBy")]
    public List<string> PartitionBy { get; set; } = new() { "custodianEmail", "date" };

    [JsonPropertyName("enableDeltaLake")]
    public bool EnableDeltaLake { get; set; } = true;

    [JsonPropertyName("dataRetentionDays")]
    public int DataRetentionDays { get; set; } = 2555; // 7 years default

    [JsonPropertyName("enableEncryption")]
    public bool EnableEncryption { get; set; } = true;

    [JsonPropertyName("notificationEndpoints")]
    public List<string> NotificationEndpoints { get; set; } = new();
}

/// <summary>
/// Priority levels for GDC pipeline execution
/// </summary>
public enum GdcPriority
{
    Low = 1,
    Normal = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

/// <summary>
/// States of GDC pipeline execution
/// </summary>
public enum GdcPipelineState
{
    Queued = 1,
    Starting = 2,
    Running = 3,
    Completing = 4,
    Succeeded = 5,
    Failed = 6,
    Cancelled = 7,
    Timeout = 8
}

/// <summary>
/// Result from GDC pipeline status query
/// </summary>
public class GdcStatusQueryResult
{
    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; set; }

    [JsonPropertyName("pipelineStatus")]
    public GdcPipelineStatus? PipelineStatus { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("lastChecked")]
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}
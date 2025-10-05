using System.Text.Json.Serialization;

namespace EDiscovery.Shared.Models;

/// <summary>
/// Structured logging events for observability and monitoring
/// </summary>
public static class ObservabilityEvents
{
    public const string JobStarted = "JobStarted";
    public const string ItemCollected = "ItemCollected";
    public const string BackoffTriggered = "BackoffTriggered";
    public const string AutoRoutedToGDC = "AutoRoutedToGDC";
    public const string JobCompleted = "JobCompleted";
    public const string HealthCheck = "HealthCheck";
    public const string MetricsSnapshot = "MetricsSnapshot";
}

/// <summary>
/// Job started event data
/// </summary>
public class JobStartedEvent
{
    [JsonPropertyName("jobId")]
    public int JobId { get; set; }

    [JsonPropertyName("custodianEmail")]
    public string CustodianEmail { get; set; } = string.Empty;

    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = string.Empty;

    [JsonPropertyName("route")]
    public string Route { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("estimatedItems")]
    public int EstimatedItems { get; set; }

    [JsonPropertyName("estimatedSizeBytes")]
    public long EstimatedSizeBytes { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "Normal";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Item collected event data
/// </summary>
public class ItemCollectedEvent
{
    [JsonPropertyName("jobId")]
    public int JobId { get; set; }

    [JsonPropertyName("custodianEmail")]
    public string CustodianEmail { get; set; } = string.Empty;

    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("collectionTimestamp")]
    public DateTime CollectionTimestamp { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("processingDurationMs")]
    public long ProcessingDurationMs { get; set; }
}

/// <summary>
/// Backoff triggered event data
/// </summary>
public class BackoffTriggeredEvent
{
    [JsonPropertyName("jobId")]
    public int JobId { get; set; }

    [JsonPropertyName("custodianEmail")]
    public string CustodianEmail { get; set; } = string.Empty;

    [JsonPropertyName("errorType")]
    public string ErrorType { get; set; } = string.Empty;

    [JsonPropertyName("httpStatusCode")]
    public int? HttpStatusCode { get; set; }

    [JsonPropertyName("retryAttempt")]
    public int RetryAttempt { get; set; }

    [JsonPropertyName("backoffDelayMs")]
    public long BackoffDelayMs { get; set; }

    [JsonPropertyName("nextRetryAt")]
    public DateTime NextRetryAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("apiEndpoint")]
    public string ApiEndpoint { get; set; } = string.Empty;
}

/// <summary>
/// Auto-routed to GDC event data
/// </summary>
public class AutoRoutedToGDCEvent
{
    [JsonPropertyName("jobId")]
    public int JobId { get; set; }

    [JsonPropertyName("custodianEmail")]
    public string CustodianEmail { get; set; } = string.Empty;

    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = string.Empty;

    [JsonPropertyName("routingReason")]
    public string RoutingReason { get; set; } = string.Empty;

    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("estimatedItems")]
    public int EstimatedItems { get; set; }

    [JsonPropertyName("estimatedSizeBytes")]
    public long EstimatedSizeBytes { get; set; }

    [JsonPropertyName("thresholds")]
    public AutoRouterThresholds Thresholds { get; set; } = new();

    [JsonPropertyName("pipelineRunId")]
    public string PipelineRunId { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "Normal";
}

/// <summary>
/// AutoRouter thresholds for observability
/// </summary>
public class AutoRouterThresholds
{
    [JsonPropertyName("maxSizeBytes")]
    public long MaxSizeBytes { get; set; }

    [JsonPropertyName("maxItemCount")]
    public int MaxItemCount { get; set; }

    [JsonPropertyName("currentQuotaUsedBytes")]
    public long CurrentQuotaUsedBytes { get; set; }

    [JsonPropertyName("currentQuotaUsedItems")]
    public int CurrentQuotaUsedItems { get; set; }
}

/// <summary>
/// Job completed event data
/// </summary>
public class JobCompletedEvent
{
    [JsonPropertyName("jobId")]
    public int JobId { get; set; }

    [JsonPropertyName("custodianEmail")]
    public string CustodianEmail { get; set; } = string.Empty;

    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = string.Empty;

    [JsonPropertyName("route")]
    public string Route { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("collectedItems")]
    public int CollectedItems { get; set; }

    [JsonPropertyName("collectedSizeBytes")]
    public long CollectedSizeBytes { get; set; }

    [JsonPropertyName("throughputItemsPerMinute")]
    public double ThroughputItemsPerMinute { get; set; }

    [JsonPropertyName("throughputMBPerMinute")]
    public double ThroughputMBPerMinute { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("throttlingEvents")]
    public int ThrottlingEvents { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("manifestGenerated")]
    public bool ManifestGenerated { get; set; }

    [JsonPropertyName("chainOfCustodySealed")]
    public bool ChainOfCustodySealed { get; set; }
}

/// <summary>
/// Health metrics for monitoring
/// </summary>
public class HealthMetrics
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("systemStatus")]
    public string SystemStatus { get; set; } = "Healthy";

    [JsonPropertyName("activeJobs")]
    public int ActiveJobs { get; set; }

    [JsonPropertyName("queuedJobs")]
    public int QueuedJobs { get; set; }

    [JsonPropertyName("completedJobsLast24h")]
    public int CompletedJobsLast24h { get; set; }

    [JsonPropertyName("failedJobsLast24h")]
    public int FailedJobsLast24h { get; set; }

    [JsonPropertyName("throughputMetrics")]
    public ThroughputMetrics ThroughputMetrics { get; set; } = new();

    [JsonPropertyName("errorMetrics")]
    public ErrorMetrics ErrorMetrics { get; set; } = new();

    [JsonPropertyName("resourceMetrics")]
    public ResourceMetrics ResourceMetrics { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public Dictionary<string, DependencyHealth> Dependencies { get; set; } = new();
}

/// <summary>
/// Throughput metrics for performance monitoring
/// </summary>
public class ThroughputMetrics
{
    [JsonPropertyName("itemsPerMinuteLast15min")]
    public double ItemsPerMinuteLast15min { get; set; }

    [JsonPropertyName("mbPerMinuteLast15min")]
    public double MBPerMinuteLast15min { get; set; }

    [JsonPropertyName("itemsPerMinuteLast1h")]
    public double ItemsPerMinuteLast1h { get; set; }

    [JsonPropertyName("mbPerMinuteLast1h")]
    public double MBPerMinuteLast1h { get; set; }

    [JsonPropertyName("peakItemsPerMinute")]
    public double PeakItemsPerMinute { get; set; }

    [JsonPropertyName("peakMBPerMinute")]
    public double PeakMBPerMinute { get; set; }

    [JsonPropertyName("averageJobDurationMinutes")]
    public double AverageJobDurationMinutes { get; set; }
}

/// <summary>
/// Error metrics for reliability monitoring
/// </summary>
public class ErrorMetrics
{
    [JsonPropertyName("throttling429CountLast15min")]
    public int Throttling429CountLast15min { get; set; }

    [JsonPropertyName("throttling429CountLast1h")]
    public int Throttling429CountLast1h { get; set; }

    [JsonPropertyName("throttling429CountLast24h")]
    public int Throttling429CountLast24h { get; set; }

    [JsonPropertyName("serverError5xxCountLast1h")]
    public int ServerError5xxCountLast1h { get; set; }

    [JsonPropertyName("authenticationErrorsLast1h")]
    public int AuthenticationErrorsLast1h { get; set; }

    [JsonPropertyName("timeoutErrorsLast1h")]
    public int TimeoutErrorsLast1h { get; set; }

    [JsonPropertyName("retrySuccessRate")]
    public double RetrySuccessRate { get; set; }

    [JsonPropertyName("averageBackoffDelayMs")]
    public double AverageBackoffDelayMs { get; set; }
}

/// <summary>
/// Resource utilization metrics
/// </summary>
public class ResourceMetrics
{
    [JsonPropertyName("cpuUsagePercent")]
    public double CpuUsagePercent { get; set; }

    [JsonPropertyName("memoryUsageMB")]
    public long MemoryUsageMB { get; set; }

    [JsonPropertyName("diskUsagePercent")]
    public double DiskUsagePercent { get; set; }

    [JsonPropertyName("networkBytesPerSecond")]
    public long NetworkBytesPerSecond { get; set; }

    [JsonPropertyName("databaseConnectionPoolSize")]
    public int DatabaseConnectionPoolSize { get; set; }

    [JsonPropertyName("httpClientActiveConnections")]
    public int HttpClientActiveConnections { get; set; }
}

/// <summary>
/// Dependency health status
/// </summary>
public class DependencyHealth
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Unknown";

    [JsonPropertyName("responseTimeMs")]
    public long ResponseTimeMs { get; set; }

    [JsonPropertyName("lastChecked")]
    public DateTime LastChecked { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
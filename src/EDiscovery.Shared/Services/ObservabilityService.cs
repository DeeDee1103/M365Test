using EDiscovery.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace EDiscovery.Shared.Services;

public interface IObservabilityService
{
    // Structured logging events
    void LogJobStarted(JobStartedEvent jobEvent, string correlationId);
    void LogItemCollected(ItemCollectedEvent itemEvent, string correlationId);
    void LogBackoffTriggered(BackoffTriggeredEvent backoffEvent, string correlationId);
    void LogAutoRoutedToGDC(AutoRoutedToGDCEvent routingEvent, string correlationId);
    void LogJobCompleted(JobCompletedEvent jobEvent, string correlationId);

    // Metrics collection
    void IncrementItemsCollected(int count = 1);
    void IncrementBytesCollected(long bytes);
    void IncrementThrottlingEvent(int statusCode);
    void IncrementServerError(int statusCode);
    void IncrementAuthError();
    void IncrementTimeoutError();
    void IncrementRetrySuccess();
    void IncrementRetryFailure();
    void RecordBackoffDelay(long delayMs);
    void RecordJobDuration(long durationMs);

    // Health and metrics retrieval
    Task<HealthMetrics> GetHealthMetricsAsync();
    Task<ThroughputMetrics> GetThroughputMetricsAsync();
    Task<ErrorMetrics> GetErrorMetricsAsync();
    void ResetMetrics();
}

public class ObservabilityService : IObservabilityService
{
    private readonly ILogger<ObservabilityService> _logger;
    private readonly IComplianceLogger _complianceLogger;

    // Metrics storage
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentQueue<MetricDataPoint> _timeSeriesData = new();
    private readonly object _metricsLock = new object();

    // Time window constants
    private readonly TimeSpan _metricsRetentionPeriod = TimeSpan.FromHours(24);
    private readonly TimeSpan _window15Minutes = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _window1Hour = TimeSpan.FromHours(1);
    private readonly TimeSpan _window24Hours = TimeSpan.FromHours(24);

    public ObservabilityService(
        ILogger<ObservabilityService> logger,
        IComplianceLogger complianceLogger)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;

        // Initialize counters
        InitializeCounters();

        // Start background cleanup task
        _ = Task.Run(CleanupOldMetricsAsync);
    }

    #region Structured Logging Events

    public void LogJobStarted(JobStartedEvent jobEvent, string correlationId)
    {
        var eventData = new
        {
            Event = ObservabilityEvents.JobStarted,
            Data = jobEvent,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _logger.LogInformation("OBSERVABILITY: {Event} | JobId: {JobId} | Custodian: {Custodian} | Route: {Route} | EstimatedItems: {Items} | EstimatedSize: {SizeBytes} bytes",
            ObservabilityEvents.JobStarted,
            jobEvent.JobId,
            jobEvent.CustodianEmail,
            jobEvent.Route,
            jobEvent.EstimatedItems,
            jobEvent.EstimatedSizeBytes);

        _complianceLogger.LogAudit(ObservabilityEvents.JobStarted, eventData, jobEvent.CustodianEmail, correlationId);

        // Record metrics
        IncrementCounter("jobs_started_total");
        IncrementCounter($"jobs_started_route_{jobEvent.Route.ToLowerInvariant()}");
    }

    public void LogItemCollected(ItemCollectedEvent itemEvent, string correlationId)
    {
        var eventData = new
        {
            Event = ObservabilityEvents.ItemCollected,
            Data = itemEvent,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _logger.LogInformation("OBSERVABILITY: {Event} | JobId: {JobId} | ItemId: {ItemId} | Type: {Type} | Size: {SizeBytes} bytes | ProcessingTime: {ProcessingMs}ms",
            ObservabilityEvents.ItemCollected,
            itemEvent.JobId,
            itemEvent.ItemId,
            itemEvent.ItemType,
            itemEvent.SizeBytes,
            itemEvent.ProcessingDurationMs);

        _complianceLogger.LogChainOfCustody(
            itemEvent.ItemId,
            "Collected",
            itemEvent.Hash,
            new { itemEvent.Source, itemEvent.ItemType, itemEvent.SizeBytes },
            correlationId);

        // Record metrics
        IncrementItemsCollected(1);
        IncrementBytesCollected(itemEvent.SizeBytes);
        RecordTimeSeriesDataPoint("items_collected", 1, DateTime.UtcNow);
        RecordTimeSeriesDataPoint("bytes_collected", itemEvent.SizeBytes, DateTime.UtcNow);
        RecordTimeSeriesDataPoint("processing_duration_ms", itemEvent.ProcessingDurationMs, DateTime.UtcNow);
    }

    public void LogBackoffTriggered(BackoffTriggeredEvent backoffEvent, string correlationId)
    {
        var eventData = new
        {
            Event = ObservabilityEvents.BackoffTriggered,
            Data = backoffEvent,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _logger.LogWarning("OBSERVABILITY: {Event} | JobId: {JobId} | ErrorType: {ErrorType} | StatusCode: {StatusCode} | Attempt: {Attempt} | BackoffDelay: {DelayMs}ms | NextRetry: {NextRetry}",
            ObservabilityEvents.BackoffTriggered,
            backoffEvent.JobId,
            backoffEvent.ErrorType,
            backoffEvent.HttpStatusCode,
            backoffEvent.RetryAttempt,
            backoffEvent.BackoffDelayMs,
            backoffEvent.NextRetryAt);

        _complianceLogger.LogAudit(ObservabilityEvents.BackoffTriggered, eventData, backoffEvent.CustodianEmail, correlationId);

        // Record metrics
        if (backoffEvent.HttpStatusCode == 429)
            IncrementThrottlingEvent(429);
        else if (backoffEvent.HttpStatusCode >= 500)
            IncrementServerError(backoffEvent.HttpStatusCode ?? 500);

        RecordBackoffDelay(backoffEvent.BackoffDelayMs);
        IncrementCounter("backoff_triggered_total");
        RecordTimeSeriesDataPoint("backoff_events", 1, DateTime.UtcNow);
    }

    public void LogAutoRoutedToGDC(AutoRoutedToGDCEvent routingEvent, string correlationId)
    {
        var eventData = new
        {
            Event = ObservabilityEvents.AutoRoutedToGDC,
            Data = routingEvent,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _logger.LogInformation("OBSERVABILITY: {Event} | JobId: {JobId} | Custodian: {Custodian} | Reason: {Reason} | Confidence: {Confidence:P1} | PipelineRunId: {PipelineRunId}",
            ObservabilityEvents.AutoRoutedToGDC,
            routingEvent.JobId,
            routingEvent.CustodianEmail,
            routingEvent.RoutingReason,
            routingEvent.ConfidenceScore,
            routingEvent.PipelineRunId);

        _complianceLogger.LogAudit(ObservabilityEvents.AutoRoutedToGDC, eventData, routingEvent.CustodianEmail, correlationId);

        // Record metrics
        IncrementCounter("autorouted_to_gdc_total");
        IncrementCounter($"autorouted_priority_{routingEvent.Priority.ToLowerInvariant()}");
        RecordTimeSeriesDataPoint("gdc_routing_events", 1, DateTime.UtcNow);
    }

    public void LogJobCompleted(JobCompletedEvent jobEvent, string correlationId)
    {
        var eventData = new
        {
            Event = ObservabilityEvents.JobCompleted,
            Data = jobEvent,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        _logger.LogInformation("OBSERVABILITY: {Event} | JobId: {JobId} | Status: {Status} | Duration: {DurationMs}ms | Items: {Items} | Size: {SizeBytes} bytes | Throughput: {ItemsPerMin:F1} items/min, {MBPerMin:F2} MB/min | Retries: {Retries} | Throttling: {Throttling} | Manifest: {Manifest}",
            ObservabilityEvents.JobCompleted,
            jobEvent.JobId,
            jobEvent.Status,
            jobEvent.DurationMs,
            jobEvent.CollectedItems,
            jobEvent.CollectedSizeBytes,
            jobEvent.ThroughputItemsPerMinute,
            jobEvent.ThroughputMBPerMinute,
            jobEvent.RetryCount,
            jobEvent.ThrottlingEvents,
            jobEvent.ManifestGenerated);

        _complianceLogger.LogAudit(ObservabilityEvents.JobCompleted, eventData, jobEvent.CustodianEmail, correlationId);

        // Record metrics
        var statusKey = jobEvent.Status.ToLowerInvariant();
        IncrementCounter($"jobs_completed_{statusKey}");
        IncrementCounter($"jobs_completed_route_{jobEvent.Route.ToLowerInvariant()}");
        
        if (statusKey == "succeeded")
        {
            IncrementCounter("jobs_completed_total");
            RecordJobDuration(jobEvent.DurationMs);
        }
        else
        {
            IncrementCounter("jobs_failed_total");
        }

        RecordTimeSeriesDataPoint("job_duration_ms", jobEvent.DurationMs, DateTime.UtcNow);
        RecordTimeSeriesDataPoint("job_items_collected", jobEvent.CollectedItems, DateTime.UtcNow);
        RecordTimeSeriesDataPoint("job_bytes_collected", jobEvent.CollectedSizeBytes, DateTime.UtcNow);
    }

    #endregion

    #region Metrics Collection

    public void IncrementItemsCollected(int count = 1)
    {
        IncrementCounter("items_collected_total", count);
    }

    public void IncrementBytesCollected(long bytes)
    {
        IncrementCounter("bytes_collected_total", bytes);
    }

    public void IncrementThrottlingEvent(int statusCode)
    {
        IncrementCounter("throttling_429_total");
        IncrementCounter($"http_errors_{statusCode}");
        RecordTimeSeriesDataPoint("throttling_events", 1, DateTime.UtcNow);
    }

    public void IncrementServerError(int statusCode)
    {
        IncrementCounter($"server_errors_5xx_total");
        IncrementCounter($"http_errors_{statusCode}");
        RecordTimeSeriesDataPoint("server_errors", 1, DateTime.UtcNow);
    }

    public void IncrementAuthError()
    {
        IncrementCounter("auth_errors_total");
        RecordTimeSeriesDataPoint("auth_errors", 1, DateTime.UtcNow);
    }

    public void IncrementTimeoutError()
    {
        IncrementCounter("timeout_errors_total");
        RecordTimeSeriesDataPoint("timeout_errors", 1, DateTime.UtcNow);
    }

    public void IncrementRetrySuccess()
    {
        IncrementCounter("retry_success_total");
    }

    public void IncrementRetryFailure()
    {
        IncrementCounter("retry_failure_total");
    }

    public void RecordBackoffDelay(long delayMs)
    {
        RecordTimeSeriesDataPoint("backoff_delay_ms", delayMs, DateTime.UtcNow);
    }

    public void RecordJobDuration(long durationMs)
    {
        RecordTimeSeriesDataPoint("job_duration_ms", durationMs, DateTime.UtcNow);
    }

    #endregion

    #region Health and Metrics Retrieval

    public async Task<HealthMetrics> GetHealthMetricsAsync()
    {
        await Task.Delay(10); // Simulate async operation

        var healthMetrics = new HealthMetrics
        {
            Timestamp = DateTime.UtcNow,
            SystemStatus = DetermineSystemStatus(),
            ActiveJobs = (int)GetCounterValue("jobs_started_total") - (int)GetCounterValue("jobs_completed_total") - (int)GetCounterValue("jobs_failed_total"),
            QueuedJobs = 0, // Would query from job queue in real implementation
            CompletedJobsLast24h = GetTimeWindowCount("jobs_completed_total", _window24Hours),
            FailedJobsLast24h = GetTimeWindowCount("jobs_failed_total", _window24Hours),
            ThroughputMetrics = await GetThroughputMetricsAsync(),
            ErrorMetrics = await GetErrorMetricsAsync(),
            ResourceMetrics = GetResourceMetrics(),
            Dependencies = await GetDependenciesHealthAsync()
        };

        // Log health metrics snapshot
        _logger.LogInformation("OBSERVABILITY: {Event} | Status: {Status} | ActiveJobs: {ActiveJobs} | Throughput: {ItemsPerMin:F1} items/min",
            ObservabilityEvents.HealthCheck,
            healthMetrics.SystemStatus,
            healthMetrics.ActiveJobs,
            healthMetrics.ThroughputMetrics.ItemsPerMinuteLast15min);

        return healthMetrics;
    }

    public async Task<ThroughputMetrics> GetThroughputMetricsAsync()
    {
        await Task.Delay(5); // Simulate async operation

        return new ThroughputMetrics
        {
            ItemsPerMinuteLast15min = GetRatePerMinute("items_collected", _window15Minutes),
            MBPerMinuteLast15min = GetRatePerMinute("bytes_collected", _window15Minutes) / (1024 * 1024),
            ItemsPerMinuteLast1h = GetRatePerMinute("items_collected", _window1Hour),
            MBPerMinuteLast1h = GetRatePerMinute("bytes_collected", _window1Hour) / (1024 * 1024),
            PeakItemsPerMinute = GetPeakRate("items_collected", _window24Hours),
            PeakMBPerMinute = GetPeakRate("bytes_collected", _window24Hours) / (1024 * 1024),
            AverageJobDurationMinutes = GetAverageValue("job_duration_ms", _window24Hours) / (1000 * 60)
        };
    }

    public async Task<ErrorMetrics> GetErrorMetricsAsync()
    {
        await Task.Delay(5); // Simulate async operation

        var retrySuccess = GetCounterValue("retry_success_total");
        var retryFailure = GetCounterValue("retry_failure_total");
        var totalRetries = retrySuccess + retryFailure;

        return new ErrorMetrics
        {
            Throttling429CountLast15min = GetTimeWindowCount("throttling_events", _window15Minutes),
            Throttling429CountLast1h = GetTimeWindowCount("throttling_events", _window1Hour),
            Throttling429CountLast24h = GetTimeWindowCount("throttling_events", _window24Hours),
            ServerError5xxCountLast1h = GetTimeWindowCount("server_errors", _window1Hour),
            AuthenticationErrorsLast1h = GetTimeWindowCount("auth_errors", _window1Hour),
            TimeoutErrorsLast1h = GetTimeWindowCount("timeout_errors", _window1Hour),
            RetrySuccessRate = totalRetries > 0 ? (double)retrySuccess / totalRetries : 1.0,
            AverageBackoffDelayMs = GetAverageValue("backoff_delay_ms", _window24Hours)
        };
    }

    public void ResetMetrics()
    {
        lock (_metricsLock)
        {
            _counters.Clear();
            InitializeCounters();
            
            // Clear time series data
            while (_timeSeriesData.TryDequeue(out _)) { }
        }

        _logger.LogInformation("OBSERVABILITY: Metrics reset completed");
    }

    #endregion

    #region Private Helper Methods

    private void InitializeCounters()
    {
        var counterNames = new[]
        {
            "jobs_started_total", "jobs_completed_total", "jobs_failed_total",
            "items_collected_total", "bytes_collected_total",
            "throttling_429_total", "server_errors_5xx_total", "auth_errors_total", "timeout_errors_total",
            "retry_success_total", "retry_failure_total", "backoff_triggered_total",
            "autorouted_to_gdc_total"
        };

        foreach (var counter in counterNames)
        {
            _counters.TryAdd(counter, 0);
        }
    }

    private void IncrementCounter(string name, long value = 1)
    {
        _counters.AddOrUpdate(name, value, (key, current) => current + value);
    }

    private long GetCounterValue(string name)
    {
        return _counters.TryGetValue(name, out var value) ? value : 0;
    }

    private void RecordTimeSeriesDataPoint(string metric, double value, DateTime timestamp)
    {
        _timeSeriesData.Enqueue(new MetricDataPoint
        {
            Metric = metric,
            Value = value,
            Timestamp = timestamp
        });
    }

    private double GetRatePerMinute(string metric, TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var dataPoints = _timeSeriesData
            .Where(dp => dp.Metric == metric && dp.Timestamp >= cutoff)
            .ToList();

        if (!dataPoints.Any()) return 0;

        var totalValue = dataPoints.Sum(dp => dp.Value);
        var windowMinutes = timeWindow.TotalMinutes;
        return totalValue / windowMinutes;
    }

    private double GetPeakRate(string metric, TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var dataPoints = _timeSeriesData
            .Where(dp => dp.Metric == metric && dp.Timestamp >= cutoff)
            .ToList();

        if (!dataPoints.Any()) return 0;

        // Group by minute and find peak
        var minuteGroups = dataPoints
            .GroupBy(dp => new DateTime(dp.Timestamp.Year, dp.Timestamp.Month, dp.Timestamp.Day, dp.Timestamp.Hour, dp.Timestamp.Minute, 0))
            .Select(g => g.Sum(dp => dp.Value));

        return minuteGroups.Any() ? minuteGroups.Max() : 0;
    }

    private double GetAverageValue(string metric, TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var dataPoints = _timeSeriesData
            .Where(dp => dp.Metric == metric && dp.Timestamp >= cutoff)
            .ToList();

        return dataPoints.Any() ? dataPoints.Average(dp => dp.Value) : 0;
    }

    private int GetTimeWindowCount(string metric, TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        return _timeSeriesData
            .Count(dp => dp.Metric == metric && dp.Timestamp >= cutoff);
    }

    private string DetermineSystemStatus()
    {
        var throttlingRate = GetRatePerMinute("throttling_events", _window15Minutes);
        var errorRate = GetRatePerMinute("server_errors", _window15Minutes);
        var activeJobs = GetCounterValue("jobs_started_total") - GetCounterValue("jobs_completed_total") - GetCounterValue("jobs_failed_total");

        if (errorRate > 10 || throttlingRate > 20)
            return "Degraded";
        
        if (activeJobs > 100) // Assuming 100 concurrent jobs is high
            return "High Load";

        return "Healthy";
    }

    private ResourceMetrics GetResourceMetrics()
    {
        var process = Process.GetCurrentProcess();
        
        return new ResourceMetrics
        {
            CpuUsagePercent = 0, // Would implement CPU monitoring
            MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
            DiskUsagePercent = 0, // Would implement disk monitoring
            NetworkBytesPerSecond = 0, // Would implement network monitoring
            DatabaseConnectionPoolSize = 0, // Would query EF connection pool
            HttpClientActiveConnections = 0 // Would query HttpClient metrics
        };
    }

    private async Task<Dictionary<string, DependencyHealth>> GetDependenciesHealthAsync()
    {
        var dependencies = new Dictionary<string, DependencyHealth>();

        // Microsoft Graph API
        dependencies["Microsoft Graph"] = new DependencyHealth
        {
            Status = "Unknown", // Would implement actual health check
            ResponseTimeMs = 0,
            LastChecked = DateTime.UtcNow,
            Version = "v1.0"
        };

        // Database
        dependencies["Database"] = new DependencyHealth
        {
            Status = "Healthy", // Would implement DB health check
            ResponseTimeMs = 5,
            LastChecked = DateTime.UtcNow,
            Version = "SQLite"
        };

        // Azure Service Bus (if configured)
        dependencies["Service Bus"] = new DependencyHealth
        {
            Status = "Not Configured",
            ResponseTimeMs = 0,
            LastChecked = DateTime.UtcNow
        };

        return dependencies;
    }

    private async Task CleanupOldMetricsAsync()
    {
        while (true)
        {
            try
            {
                var cutoff = DateTime.UtcNow - _metricsRetentionPeriod;
                var itemsToRemove = new List<MetricDataPoint>();

                // Collect old items
                while (_timeSeriesData.TryPeek(out var item) && item.Timestamp < cutoff)
                {
                    if (_timeSeriesData.TryDequeue(out var removedItem))
                        itemsToRemove.Add(removedItem);
                }

                if (itemsToRemove.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} old metric data points", itemsToRemove.Count);
                }

                await Task.Delay(TimeSpan.FromMinutes(5)); // Clean up every 5 minutes
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics cleanup");
                await Task.Delay(TimeSpan.FromMinutes(1)); // Retry after 1 minute on error
            }
        }
    }

    #endregion
}

/// <summary>
/// Time series data point for metrics
/// </summary>
internal class MetricDataPoint
{
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}
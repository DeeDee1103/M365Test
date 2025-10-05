using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HybridGraphCollectorWorker.Services;

/// <summary>
/// Temporary observability helper for structured logging until ObservabilityService compilation issues are resolved
/// </summary>
public class ObservabilityHelper
{
    private readonly ILogger _logger;
    private readonly IComplianceLogger _complianceLogger;

    public ObservabilityHelper(ILogger logger, IComplianceLogger complianceLogger)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;
    }

    /// <summary>
    /// Log structured JobStarted event
    /// </summary>
    public void LogJobStarted(CollectionRequest request, string correlationId)
    {
        var jobEvent = new
        {
            EventType = "JobStarted",
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType.ToString(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Keywords = request.Keywords,
            IncludeAttachments = request.IncludeAttachments,
            OutputPath = request.OutputPath,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        var jsonEvent = JsonSerializer.Serialize(jobEvent);
        _logger.LogInformation("JobStarted | {JobStartedEvent}", jsonEvent);
        _complianceLogger.LogAudit("JobStarted", jobEvent, null, correlationId);
    }

    /// <summary>
    /// Log structured ItemCollected event
    /// </summary>
    public void LogItemCollected(string itemId, string itemType, long sizeBytes, string custodianEmail, string correlationId)
    {
        var itemEvent = new
        {
            EventType = "ItemCollected",
            ItemId = itemId,
            ItemType = itemType,
            SizeBytes = sizeBytes,
            CustodianEmail = custodianEmail,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        var jsonEvent = JsonSerializer.Serialize(itemEvent);
        _logger.LogInformation("ItemCollected | {ItemCollectedEvent}", jsonEvent);
        _complianceLogger.LogAudit("ItemCollected", itemEvent, null, correlationId);
    }

    /// <summary>
    /// Log structured BackoffTriggered event
    /// </summary>
    public void LogBackoffTriggered(int statusCode, long delayMs, string endpoint, string correlationId)
    {
        var backoffEvent = new
        {
            EventType = "BackoffTriggered",
            StatusCode = statusCode,
            DelayMs = delayMs,
            Endpoint = endpoint,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        var jsonEvent = JsonSerializer.Serialize(backoffEvent);
        _logger.LogWarning("BackoffTriggered | {BackoffTriggeredEvent}", jsonEvent);
        _complianceLogger.LogAudit("BackoffTriggered", backoffEvent, null, correlationId);
    }

    /// <summary>
    /// Log structured AutoRoutedToGDC event
    /// </summary>
    public void LogAutoRoutedToGDC(CollectionRequest request, AutoRouterDecision decision, string correlationId)
    {
        var routingEvent = new
        {
            EventType = "AutoRoutedToGDC",
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType.ToString(),
            RecommendedRoute = decision.RecommendedRoute.ToString(),
            Reason = decision.Reason,
            ConfidenceScore = decision.ConfidenceScore,
            EstimatedDataSizeBytes = decision.EstimatedDataSizeBytes,
            EstimatedItemCount = decision.EstimatedItemCount,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        var jsonEvent = JsonSerializer.Serialize(routingEvent);
        _logger.LogInformation("AutoRoutedToGDC | {AutoRoutedToGDCEvent}", jsonEvent);
        _complianceLogger.LogAudit("AutoRoutedToGDC", routingEvent, null, correlationId);
    }

    /// <summary>
    /// Log structured JobCompleted event
    /// </summary>
    public void LogJobCompleted(CollectionRequest request, CollectionResult result, TimeSpan duration, string correlationId)
    {
        var jobEvent = new
        {
            EventType = "JobCompleted",
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType.ToString(),
            IsSuccessful = result.IsSuccessful,
            ItemCount = result.TotalItemCount,
            SizeBytes = result.TotalSizeBytes,
            DurationMs = (long)duration.TotalMilliseconds,
            ErrorMessage = result.ErrorMessage,
            StartTime = result.StartTime,
            EndTime = result.EndTime,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        var jsonEvent = JsonSerializer.Serialize(jobEvent);
        
        if (result.IsSuccessful)
        {
            _logger.LogInformation("JobCompleted | {JobCompletedEvent}", jsonEvent);
        }
        else
        {
            _logger.LogError("JobCompleted | {JobCompletedEvent}", jsonEvent);
        }
        
        _complianceLogger.LogAudit("JobCompleted", jobEvent, null, correlationId);
    }

    /// <summary>
    /// Get simple metrics for health endpoint
    /// </summary>
    public object GetSimpleMetrics()
    {
        return new
        {
            Timestamp = DateTime.UtcNow,
            Status = "Healthy",
            Uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,
            Version = "2.1.0",
            Service = "Hybrid Graph Collector Worker",
            // Additional metrics can be added as needed
            ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
            WorkingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64
        };
    }
}
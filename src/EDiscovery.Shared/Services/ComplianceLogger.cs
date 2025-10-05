using EDiscovery.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Specialized logging service for eDiscovery compliance and audit requirements.
/// Provides structured logging with correlation IDs, performance metrics, and chain of custody tracking.
/// </summary>
public interface IComplianceLogger
{
    /// <summary>
    /// Log an audit event with correlation tracking
    /// </summary>
    void LogAudit(string action, object? data = null, string? custodian = null, string? correlationId = null);
    
    /// <summary>
    /// Log a security-related event
    /// </summary>
    void LogSecurity(string securityEvent, object? data = null, string? correlationId = null);
    
    /// <summary>
    /// Log a chain of custody event for evidence integrity
    /// </summary>
    void LogChainOfCustody(string itemId, string action, string hash, object? metadata = null, string? correlationId = null);
    
    /// <summary>
    /// Log performance metrics for collection operations
    /// </summary>
    void LogPerformance(string operation, long durationMs, long itemCount = 0, long sizeBytes = 0, string? correlationId = null);
    
    /// <summary>
    /// Log data collection activity with privacy considerations
    /// </summary>
    void LogDataCollection(string custodian, CollectionJobType jobType, CollectionRoute route, long itemCount, long sizeBytes, string? correlationId = null);
    
    /// <summary>
    /// Log error with full context and correlation
    /// </summary>
    void LogError(Exception exception, string context, object? additionalData = null, string? correlationId = null);
    
    /// <summary>
    /// Log Microsoft Graph API interactions for quota tracking
    /// </summary>
    void LogGraphApiCall(string endpoint, string method, int statusCode, long responseTimeMs, string? quotaInfo = null, string? correlationId = null);
    
    /// <summary>
    /// Create a new correlation ID for tracking related operations
    /// </summary>
    string CreateCorrelationId();
    
    /// <summary>
    /// Start a performance timer for an operation
    /// </summary>
    IDisposable StartPerformanceTimer(string operation, string? correlationId = null);
}

public class ComplianceLogger : IComplianceLogger
{
    private readonly ILogger<ComplianceLogger> _logger;
    private readonly string _instanceId;

    public ComplianceLogger(ILogger<ComplianceLogger> logger)
    {
        _logger = logger;
        _instanceId = Environment.MachineName + "-" + Process.GetCurrentProcess().Id;
    }

    public string CreateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..8]; // Short correlation ID
    }

    public void LogAudit(string action, object? data = null, string? custodian = null, string? correlationId = null)
    {
        var auditData = new
        {
            Action = action,
            Custodian = custodian,
            Data = data,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId()
        };

        _logger.LogInformation("AUDIT: {Action} | Custodian: {Custodian} | CorrelationId: {CorrelationId} | Data: {AuditData}",
            action, custodian, auditData.CorrelationId, JsonSerializer.Serialize(auditData));
    }

    public void LogSecurity(string securityEvent, object? data = null, string? correlationId = null)
    {
        var securityData = new
        {
            Event = securityEvent,
            Data = data,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId(),
            UserContext = Environment.UserName,
            ProcessId = Process.GetCurrentProcess().Id
        };

        _logger.LogWarning("SECURITY: {SecurityEvent} | CorrelationId: {CorrelationId} | User: {UserContext} | Data: {SecurityData}",
            securityEvent, securityData.CorrelationId, securityData.UserContext, JsonSerializer.Serialize(securityData));
    }

    public void LogChainOfCustody(string itemId, string action, string hash, object? metadata = null, string? correlationId = null)
    {
        var custodyData = new
        {
            ItemId = itemId,
            Action = action,
            Hash = hash,
            Metadata = metadata,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId()
        };

        _logger.LogInformation("CHAIN_OF_CUSTODY: {Action} | ItemId: {ItemId} | Hash: {Hash} | CorrelationId: {CorrelationId} | Data: {CustodyData}",
            action, itemId, hash, custodyData.CorrelationId, JsonSerializer.Serialize(custodyData));
    }

    public void LogPerformance(string operation, long durationMs, long itemCount = 0, long sizeBytes = 0, string? correlationId = null)
    {
        var performanceData = new
        {
            Operation = operation,
            DurationMs = durationMs,
            ItemCount = itemCount,
            SizeBytes = sizeBytes,
            ThroughputItemsPerSecond = durationMs > 0 ? (itemCount * 1000.0 / durationMs) : 0,
            ThroughputMBPerSecond = durationMs > 0 ? (sizeBytes / 1024.0 / 1024.0 * 1000.0 / durationMs) : 0,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId()
        };

        _logger.LogInformation("PERFORMANCE: {Operation} | Duration: {DurationMs}ms | Items: {ItemCount} | Size: {SizeBytes} bytes | CorrelationId: {CorrelationId} | Throughput: {ThroughputItemsPerSecond:F2} items/sec, {ThroughputMBPerSecond:F2} MB/sec",
            operation, durationMs, itemCount, sizeBytes, performanceData.CorrelationId, 
            performanceData.ThroughputItemsPerSecond, performanceData.ThroughputMBPerSecond);
    }

    public void LogDataCollection(string custodian, CollectionJobType jobType, CollectionRoute route, long itemCount, long sizeBytes, string? correlationId = null)
    {
        var collectionData = new
        {
            Custodian = custodian,
            JobType = jobType.ToString(),
            Route = route.ToString(),
            ItemCount = itemCount,
            SizeBytes = sizeBytes,
            SizeMB = sizeBytes / 1024.0 / 1024.0,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId()
        };

        _logger.LogInformation("DATA_COLLECTION: {Custodian} | Type: {JobType} | Route: {Route} | Items: {ItemCount} | Size: {SizeMB:F2} MB | CorrelationId: {CorrelationId}",
            custodian, jobType, route, itemCount, collectionData.SizeMB, collectionData.CorrelationId);

        // Also log as audit event for compliance
        LogAudit("DataCollected", collectionData, custodian, correlationId);
    }

    public void LogError(Exception exception, string context, object? additionalData = null, string? correlationId = null)
    {
        var errorData = new
        {
            Context = context,
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            AdditionalData = additionalData,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId()
        };

        _logger.LogError(exception, "ERROR: {Context} | Exception: {ExceptionType} | CorrelationId: {CorrelationId} | Data: {ErrorData}",
            context, exception.GetType().Name, errorData.CorrelationId, JsonSerializer.Serialize(errorData));
    }

    public void LogGraphApiCall(string endpoint, string method, int statusCode, long responseTimeMs, string? quotaInfo = null, string? correlationId = null)
    {
        var apiData = new
        {
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            QuotaInfo = quotaInfo,
            Instance = _instanceId,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId ?? CreateCorrelationId()
        };

        var logLevel = statusCode >= 400 ? Microsoft.Extensions.Logging.LogLevel.Warning : Microsoft.Extensions.Logging.LogLevel.Information;
        
        _logger.Log(logLevel, "GRAPH_API: {Method} {Endpoint} | Status: {StatusCode} | Duration: {ResponseTimeMs}ms | CorrelationId: {CorrelationId} | Quota: {QuotaInfo}",
            method, endpoint, statusCode, responseTimeMs, apiData.CorrelationId, quotaInfo ?? "N/A");
    }

    public IDisposable StartPerformanceTimer(string operation, string? correlationId = null)
    {
        return new PerformanceTimer(this, operation, correlationId);
    }

    private class PerformanceTimer : IDisposable
    {
        private readonly ComplianceLogger _logger;
        private readonly string _operation;
        private readonly string _correlationId;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public PerformanceTimer(ComplianceLogger logger, string operation, string? correlationId)
        {
            _logger = logger;
            _operation = operation;
            _correlationId = correlationId ?? logger.CreateCorrelationId();
            _stopwatch = Stopwatch.StartNew();
            
            _logger._logger.LogDebug("PERFORMANCE_START: {Operation} | CorrelationId: {CorrelationId}", operation, _correlationId);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _logger.LogPerformance(_operation, _stopwatch.ElapsedMilliseconds, 0, 0, _correlationId);
                _disposed = true;
            }
        }
    }
}
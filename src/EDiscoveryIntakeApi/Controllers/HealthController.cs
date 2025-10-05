using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Data;
using Microsoft.AspNetCore.Mvc;

namespace EDiscoveryIntakeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly EDiscoveryDbContext _context;

    public HealthController(
        ILogger<HealthController> logger,
        EDiscoveryDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Simple health check endpoint for load balancer probes
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetHealthStatus()
    {
        try
        {
            // Quick database connectivity check
            var canConnectToDb = await CanConnectToDatabaseAsync();
            
            var simpleHealth = new
            {
                status = canConnectToDb ? "healthy" : "unhealthy",
                timestamp = DateTime.UtcNow,
                version = "2.1.0",
                service = "eDiscovery Intake API"
            };

            var statusCode = canConnectToDb ? 200 : 503;
            return StatusCode(statusCode, simpleHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                error = "Health check failed"
            });
        }
    }

    /// <summary>
    /// Detailed health metrics for monitoring dashboards
    /// </summary>
    [HttpGet("detailed")]
    public async Task<ActionResult> GetDetailedHealth()
    {
        try
        {
            var dbHealthy = await CanConnectToDatabaseAsync();
            
            var healthMetrics = new
            {
                Status = dbHealthy ? "Healthy" : "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = "2.1.0",
                Service = "eDiscovery Intake API",
                Dependencies = new
                {
                    Database = new { Status = dbHealthy ? "Healthy" : "Unhealthy", ResponseTime = "< 100ms" }
                },
                System = new
                {
                    Uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,
                    ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    WorkingSetMB = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024
                }
            };
            
            return Ok(healthMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve detailed health metrics");
            return StatusCode(500, new { error = "Failed to retrieve health metrics" });
        }
    }

    /// <summary>
    /// Current throughput metrics for performance monitoring
    /// </summary>
    [HttpGet("metrics/throughput")]
    public async Task<ActionResult> GetThroughputMetrics()
    {
        try
        {
            // Simplified throughput metrics
            var metrics = new
            {
                ItemsPerMinute = 0.0,
                MBPerMinute = 0.0,
                PeakItemsPerMinute = 0.0,
                PeakMBPerMinute = 0.0,
                AverageJobDurationMinutes = 0.0,
                Timestamp = DateTime.UtcNow,
                Note = "Full metrics available when ObservabilityService is enabled"
            };
            
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve throughput metrics");
            return StatusCode(500, new { error = "Failed to retrieve throughput metrics" });
        }
    }

    /// <summary>
    /// Error and reliability metrics for alerting
    /// </summary>
    [HttpGet("metrics/errors")]
    public async Task<ActionResult> GetErrorMetrics()
    {
        try
        {
            // Simplified error metrics
            var metrics = new
            {
                Throttling429Count15min = 0,
                Throttling429Count1h = 0,
                Throttling429Count24h = 0,
                RetrySuccessRate = 100.0,
                AverageBackoffDelayMs = 0.0,
                Timestamp = DateTime.UtcNow,
                Note = "Full metrics available when ObservabilityService is enabled"
            };
            
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve error metrics");
            return StatusCode(500, new { error = "Failed to retrieve error metrics" });
        }
    }

    /// <summary>
    /// System performance counters for dashboard display
    /// </summary>
    [HttpGet("counters")]
    public async Task<IActionResult> GetPerformanceCounters()
    {
        try
        {
            // Simplified performance counters for dashboard
            var counters = new
            {
                // Core throughput metrics (placeholder values)
                itemsPerMinute = 0.0,
                mbPerMinute = 0.0,
                
                // Peak performance
                peakItemsPerMinute = 0.0,
                peakMBPerMinute = 0.0,
                
                // Error rates (placeholder values)
                throttling429Count15min = 0,
                throttling429Count1h = 0,
                throttling429Count24h = 0,
                
                // Reliability metrics
                retrySuccessRate = 100.0,
                averageBackoffDelayMs = 0.0,
                averageJobDurationMinutes = 0.0,
                
                // System info
                timestamp = DateTime.UtcNow,
                uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,
                note = "Counters will be populated when ObservabilityService is fully integrated"
            };

            return Ok(counters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve performance counters");
            return StatusCode(500, new { error = "Failed to retrieve performance counters" });
        }
    }

    /// <summary>
    /// Readiness probe for Kubernetes deployments
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadinessStatus()
    {
        try
        {
            // Check if the service is ready to handle requests
            var isReady = await IsServiceReadyAsync();
            
            if (isReady)
            {
                return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
            }
            else
            {
                return StatusCode(503, new { status = "not ready", timestamp = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(503, new { status = "not ready", error = ex.Message });
        }
    }

    /// <summary>
    /// Liveness probe for Kubernetes deployments
    /// </summary>
    [HttpGet("live")]
    public IActionResult GetLivenessStatus()
    {
        // Simple liveness check - if we can respond, we're alive
        return Ok(new { 
            status = "alive", 
            timestamp = DateTime.UtcNow,
            processId = System.Diagnostics.Process.GetCurrentProcess().Id
        });
    }

    /// <summary>
    /// Reset metrics - useful for testing and debugging
    /// </summary>
    [HttpPost("metrics/reset")]
    public IActionResult ResetMetrics()
    {
        try
        {
            // Placeholder for metrics reset functionality
            _logger.LogInformation("Metrics reset requested via API endpoint (ObservabilityService not yet integrated)");
            
            return Ok(new { 
                message = "Metrics reset placeholder - full functionality available when ObservabilityService is integrated", 
                timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset metrics");
            return StatusCode(500, new { error = "Failed to reset metrics" });
        }
    }

    private async Task<bool> CanConnectToDatabaseAsync()
    {
        try
        {
            // Simple database connectivity test
            await _context.Database.CanConnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database connectivity check failed");
            return false;
        }
    }

    private async Task<bool> IsServiceReadyAsync()
    {
        try
        {
            // Check database connectivity
            var dbReady = await CanConnectToDatabaseAsync();
            
            // Check if critical services are initialized
            var servicesReady = true; // Basic readiness check
            
            // Add more readiness checks as needed
            // - Configuration loaded
            // - External dependencies available
            // - Initialization complete
            
            return dbReady && servicesReady;
        }
        catch
        {
            return false;
        }
    }
}
using Microsoft.AspNetCore.Mvc;using Microsoft.AspNetCore.Mvc;using EDiscovery.Shared.Models;

using EDiscoveryIntakeApi.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;using EDiscoveryIntakeApi.Services;using EDiscovery.Shared.Services;



namespace EDiscoveryIntakeApi.Controllers;using Microsoft.Extensions.Diagnostics.HealthChecks;using EDiscovery.Shared.Data;



/// <summary>using Microsoft.AspNetCore.Mvc;

/// Health monitoring and telemetry endpoints for eDiscovery API

/// Provides comprehensive system health, metrics, and monitoring capabilitiesnamespace EDiscoveryIntakeApi.Controllers;

/// </summary>

[ApiController]namespace EDiscoveryIntakeApi.Controllers;

[Route("api/[controller]")]

public class HealthController : ControllerBase/// <summary>

{

    private readonly EDiscoveryHealthService _healthService;/// Health monitoring and telemetry endpoints for eDiscovery API[ApiController]

    private readonly HealthCheckService _healthCheckService;

    private readonly ILogger<HealthController> _logger;/// Provides comprehensive system health, metrics, and monitoring capabilities[Route("api/[controller]")]



    public HealthController(/// </summary>public class HealthController : ControllerBase

        EDiscoveryHealthService healthService,

        HealthCheckService healthCheckService,[ApiController]{

        ILogger<HealthController> logger)

    {[Route("api/[controller]")]    private readonly ILogger<HealthController> _logger;

        _healthService = healthService;

        _healthCheckService = healthCheckService;public class HealthController : ControllerBase    private readonly EDiscoveryDbContext _context;

        _logger = logger;

    }{



    /// <summary>    private readonly EDiscoveryHealthService _healthService;    public HealthController(

    /// Simple health check endpoint for load balancers

    /// Returns 200 OK if the service is healthy    private readonly HealthCheckService _healthCheckService;        ILogger<HealthController> logger,

    /// </summary>

    [HttpGet]    private readonly ILogger<HealthController> _logger;        EDiscoveryDbContext context)

    public async Task<IActionResult> GetHealth()

    {    {

        try

        {    public HealthController(        _logger = logger;

            var healthReport = await _healthCheckService.CheckHealthAsync();

                    EDiscoveryHealthService healthService,        _context = context;

            if (healthReport.Status == HealthStatus.Healthy)

            {        HealthCheckService healthCheckService,    }

                return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });

            }        ILogger<HealthController> logger)

            else if (healthReport.Status == HealthStatus.Degraded)

            {    {    /// <summary>

                return Ok(new { Status = "Degraded", Timestamp = DateTime.UtcNow });

            }        _healthService = healthService;    /// Simple health check endpoint for load balancer probes

            else

            {        _healthCheckService = healthCheckService;    /// </summary>

                return StatusCode(503, new { Status = "Unhealthy", Timestamp = DateTime.UtcNow });

            }        _logger = logger;    [HttpGet]

        }

        catch (Exception ex)    }    public async Task<IActionResult> GetHealthStatus()

        {

            _logger.LogError(ex, "Health check failed");    {

            return StatusCode(503, new { Status = "Unhealthy", Error = "Health check failed", Timestamp = DateTime.UtcNow });

        }    /// <summary>        try

    }

    /// Simple health check endpoint for load balancers        {

    /// <summary>

    /// Detailed health check with component-level status    /// Returns 200 OK if the service is healthy            // Quick database connectivity check

    /// Returns comprehensive health information for monitoring dashboards

    /// </summary>    /// </summary>            var canConnectToDb = await CanConnectToDatabaseAsync();

    [HttpGet("detailed")]

    public async Task<IActionResult> GetDetailedHealth()    [HttpGet]            

    {

        try    public async Task<IActionResult> GetHealth()            var simpleHealth = new

        {

            var detailedHealth = await _healthService.GetDetailedHealthAsync();    {            {

            return Ok(detailedHealth);

        }        try                status = canConnectToDb ? "healthy" : "unhealthy",

        catch (Exception ex)

        {        {                timestamp = DateTime.UtcNow,

            _logger.LogError(ex, "Detailed health check failed");

            return StatusCode(500, new {             var healthReport = await _healthCheckService.CheckHealthAsync();                version = "2.1.0",

                Status = "Error", 

                Message = "Detailed health check failed",                             service = "eDiscovery Intake API"

                Timestamp = DateTime.UtcNow 

            });            if (healthReport.Status == HealthStatus.Healthy)            };

        }

    }            {



    /// <summary>                return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });            var statusCode = canConnectToDb ? 200 : 503;

    /// Performance counters and metrics endpoint

    /// Returns real-time performance metrics for monitoring systems            }            return StatusCode(statusCode, simpleHealth);

    /// </summary>

    [HttpGet("counters")]            else if (healthReport.Status == HealthStatus.Degraded)        }

    public IActionResult GetCounters()

    {            {        catch (Exception ex)

        try

        {                return Ok(new { Status = "Degraded", Timestamp = DateTime.UtcNow });        {

            var metrics = EDiscoveryHealthService.GetMetrics();

            return Ok(metrics);            }            _logger.LogError(ex, "Health check failed");

        }

        catch (Exception ex)            else            return StatusCode(503, new

        {

            _logger.LogError(ex, "Failed to retrieve performance counters");            {            {

            return StatusCode(500, new { 

                Status = "Error",                 return StatusCode(503, new { Status = "Unhealthy", Timestamp = DateTime.UtcNow });                status = "unhealthy",

                Message = "Failed to retrieve performance counters", 

                Timestamp = DateTime.UtcNow             }                timestamp = DateTime.UtcNow,

            });

        }        }                error = "Health check failed"

    }

        catch (Exception ex)            });

    /// <summary>

    /// Application information endpoint        {        }

    /// Returns version and environment information

    /// </summary>            _logger.LogError(ex, "Health check failed");    }

    [HttpGet("info")]

    public IActionResult GetInfo()            return StatusCode(503, new { Status = "Unhealthy", Error = "Health check failed", Timestamp = DateTime.UtcNow });

    {

        try        }    /// <summary>

        {

            var info = new    }    /// Detailed health metrics for monitoring dashboards

            {

                Service = "eDiscovery Intake API",    /// </summary>

                Version = "2.4.0",

                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",    /// <summary>    [HttpGet("detailed")]

                MachineName = Environment.MachineName,

                ProcessId = Environment.ProcessId,    /// Detailed health check with component-level status    public async Task<ActionResult> GetDetailedHealth()

                FrameworkVersion = Environment.Version.ToString(),

                Platform = Environment.OSVersion.ToString(),    /// Returns comprehensive health information for monitoring dashboards    {

                StartTime = System.Diagnostics.Process.GetCurrentProcess().StartTime,

                Timestamp = DateTime.UtcNow,    /// </summary>        try

                Features = new[]

                {    [HttpGet("detailed")]        {

                    "Multi-User Concurrent Processing",

                    "Job Sharding with Checkpoint Recovery",     public async Task<IActionResult> GetDetailedHealth()            var dbHealthy = await CanConnectToDatabaseAsync();

                    "Azure Key Vault Integration",

                    "Delta Query Support",    {            

                    "Chain of Custody",

                    "GDC Binary Fetch",        try            var healthMetrics = new

                    "Reconciliation Validation",

                    "Structured Telemetry"        {            {

                }

            };            var detailedHealth = await _healthService.GetDetailedHealthAsync();                Status = dbHealthy ? "Healthy" : "Unhealthy",

            

            return Ok(info);            return Ok(detailedHealth);                Timestamp = DateTime.UtcNow,

        }

        catch (Exception ex)        }                Version = "2.1.0",

        {

            _logger.LogError(ex, "Failed to retrieve application info");        catch (Exception ex)                Service = "eDiscovery Intake API",

            return StatusCode(500, new { 

                Status = "Error",         {                Dependencies = new

                Message = "Failed to retrieve application info", 

                Timestamp = DateTime.UtcNow             _logger.LogError(ex, "Detailed health check failed");                {

            });

        }            return StatusCode(500, new {                     Database = new { Status = dbHealthy ? "Healthy" : "Unhealthy", ResponseTime = "< 100ms" }

    }

}                Status = "Error",                 },

                Message = "Detailed health check failed",                 System = new

                Timestamp = DateTime.UtcNow                 {

            });                    Uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,

        }                    ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,

    }                    WorkingSetMB = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024

                }

    /// <summary>            };

    /// Performance counters and metrics endpoint            

    /// Returns real-time performance metrics for monitoring systems            return Ok(healthMetrics);

    /// </summary>        }

    [HttpGet("counters")]        catch (Exception ex)

    public IActionResult GetCounters()        {

    {            _logger.LogError(ex, "Failed to retrieve detailed health metrics");

        try            return StatusCode(500, new { error = "Failed to retrieve health metrics" });

        {        }

            var metrics = EDiscoveryHealthService.GetMetrics();    }

            return Ok(metrics);

        }    /// <summary>

        catch (Exception ex)    /// Current throughput metrics for performance monitoring

        {    /// </summary>

            _logger.LogError(ex, "Failed to retrieve performance counters");    [HttpGet("metrics/throughput")]

            return StatusCode(500, new {     public ActionResult GetThroughputMetrics()

                Status = "Error",     {

                Message = "Failed to retrieve performance counters",         try

                Timestamp = DateTime.UtcNow         {

            });            // Simplified throughput metrics

        }            var metrics = new

    }            {

                ItemsPerMinute = 0.0,

    /// <summary>                MBPerMinute = 0.0,

    /// Kubernetes readiness probe endpoint                PeakItemsPerMinute = 0.0,

    /// Indicates if the service is ready to accept traffic                PeakMBPerMinute = 0.0,

    /// </summary>                AverageJobDurationMinutes = 0.0,

    [HttpGet("ready")]                Timestamp = DateTime.UtcNow,

    public async Task<IActionResult> GetReadiness()                Note = "Full metrics available when ObservabilityService is enabled"

    {            };

        try            

        {            return Ok(metrics);

            var healthReport = await _healthCheckService.CheckHealthAsync();        }

                    catch (Exception ex)

            // Consider service ready if not completely unhealthy        {

            if (healthReport.Status != HealthStatus.Unhealthy)            _logger.LogError(ex, "Failed to retrieve throughput metrics");

            {            return StatusCode(500, new { error = "Failed to retrieve throughput metrics" });

                return Ok(new {         }

                    Status = "Ready",     }

                    Timestamp = DateTime.UtcNow,

                    Checks = healthReport.Entries.Count,    /// <summary>

                    Healthy = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),    /// Error and reliability metrics for alerting

                    Degraded = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),    /// </summary>

                    Unhealthy = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy)    [HttpGet("metrics/errors")]

                });    public ActionResult GetErrorMetrics()

            }    {

            else        try

            {        {

                return StatusCode(503, new {             // Simplified error metrics

                    Status = "NotReady",             var metrics = new

                    Timestamp = DateTime.UtcNow,            {

                    Issues = healthReport.Entries                Throttling429Count15min = 0,

                        .Where(e => e.Value.Status == HealthStatus.Unhealthy)                Throttling429Count1h = 0,

                        .Select(e => new { Component = e.Key, Description = e.Value.Description })                Throttling429Count24h = 0,

                });                RetrySuccessRate = 100.0,

            }                AverageBackoffDelayMs = 0.0,

        }                Timestamp = DateTime.UtcNow,

        catch (Exception ex)                Note = "Full metrics available when ObservabilityService is enabled"

        {            };

            _logger.LogError(ex, "Readiness check failed");            

            return StatusCode(503, new { Status = "NotReady", Error = "Readiness check failed", Timestamp = DateTime.UtcNow });            return Ok(metrics);

        }        }

    }        catch (Exception ex)

        {

    /// <summary>            _logger.LogError(ex, "Failed to retrieve error metrics");

    /// Kubernetes liveness probe endpoint            return StatusCode(500, new { error = "Failed to retrieve error metrics" });

    /// Indicates if the service is alive and should not be restarted        }

    /// </summary>    }

    [HttpGet("live")]

    public IActionResult GetLiveness()    /// <summary>

    {    /// System performance counters for dashboard display

        try    /// </summary>

        {    [HttpGet("counters")]

            // Simple liveness check - if we can respond, we're alive    public IActionResult GetPerformanceCounters()

            var appHealth = _healthService.CheckApplicationHealth();    {

                    try

            if (appHealth.Status != HealthStatus.Unhealthy)        {

            {            // Simplified performance counters for dashboard

                return Ok(new {             var counters = new

                    Status = "Alive",             {

                    Timestamp = DateTime.UtcNow,                // Core throughput metrics (placeholder values)

                    Uptime = appHealth.Data?["Uptime"],                itemsPerMinute = 0.0,

                    ProcessId = appHealth.Data?["ProcessId"]                mbPerMinute = 0.0,

                });                

            }                // Peak performance

            else                peakItemsPerMinute = 0.0,

            {                peakMBPerMinute = 0.0,

                return StatusCode(503, new {                 

                    Status = "Dead",                 // Error rates (placeholder values)

                    Timestamp = DateTime.UtcNow,                throttling429Count15min = 0,

                    Description = appHealth.Description                throttling429Count1h = 0,

                });                throttling429Count24h = 0,

            }                

        }                // Reliability metrics

        catch (Exception ex)                retrySuccessRate = 100.0,

        {                averageBackoffDelayMs = 0.0,

            _logger.LogError(ex, "Liveness check failed");                averageJobDurationMinutes = 0.0,

            return StatusCode(503, new { Status = "Dead", Error = "Liveness check failed", Timestamp = DateTime.UtcNow });                

        }                // System info

    }                timestamp = DateTime.UtcNow,

                uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime,

    /// <summary>                note = "Counters will be populated when ObservabilityService is fully integrated"

    /// Update a performance metric (for internal use by services)            };

    /// Used by services to report performance data

    /// </summary>            return Ok(counters);

    [HttpPost("metrics/{key}")]        }

    public IActionResult UpdateMetric(string key, [FromBody] object value)        catch (Exception ex)

    {        {

        try            _logger.LogError(ex, "Failed to retrieve performance counters");

        {            return StatusCode(500, new { error = "Failed to retrieve performance counters" });

            EDiscoveryHealthService.UpdateMetric(key, value);        }

            _logger.LogDebug("Updated metric: {Key} = {Value}", key, value);    }

            return Ok(new { Message = "Metric updated", Key = key, Timestamp = DateTime.UtcNow });

        }    /// <summary>

        catch (Exception ex)    /// Readiness probe for Kubernetes deployments

        {    /// </summary>

            _logger.LogError(ex, "Failed to update metric: {Key}", key);    [HttpGet("ready")]

            return StatusCode(500, new {     public async Task<IActionResult> GetReadinessStatus()

                Status = "Error",     {

                Message = "Failed to update metric",         try

                Key = key,        {

                Timestamp = DateTime.UtcNow             // Check if the service is ready to handle requests

            });            var isReady = await IsServiceReadyAsync();

        }            

    }            if (isReady)

            {

    /// <summary>                return Ok(new { status = "ready", timestamp = DateTime.UtcNow });

    /// Application information endpoint            }

    /// Returns version and environment information            else

    /// </summary>            {

    [HttpGet("info")]                return StatusCode(503, new { status = "not ready", timestamp = DateTime.UtcNow });

    public IActionResult GetInfo()            }

    {        }

        try        catch (Exception ex)

        {        {

            var info = new            _logger.LogError(ex, "Readiness check failed");

            {            return StatusCode(503, new { status = "not ready", error = ex.Message });

                Service = "eDiscovery Intake API",        }

                Version = "2.4.0",    }

                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",

                MachineName = Environment.MachineName,    /// <summary>

                ProcessId = Environment.ProcessId,    /// Liveness probe for Kubernetes deployments

                FrameworkVersion = Environment.Version.ToString(),    /// </summary>

                Platform = Environment.OSVersion.ToString(),    [HttpGet("live")]

                StartTime = System.Diagnostics.Process.GetCurrentProcess().StartTime,    public IActionResult GetLivenessStatus()

                Timestamp = DateTime.UtcNow,    {

                Features = new[]        // Simple liveness check - if we can respond, we're alive

                {        return Ok(new { 

                    "Multi-User Concurrent Processing",            status = "alive", 

                    "Job Sharding with Checkpoint Recovery",             timestamp = DateTime.UtcNow,

                    "Azure Key Vault Integration",            processId = System.Diagnostics.Process.GetCurrentProcess().Id

                    "Delta Query Support",        });

                    "Chain of Custody",    }

                    "GDC Binary Fetch",

                    "Reconciliation Validation",    /// <summary>

                    "Structured Telemetry"    /// Reset metrics - useful for testing and debugging

                }    /// </summary>

            };    [HttpPost("metrics/reset")]

                public IActionResult ResetMetrics()

            return Ok(info);    {

        }        try

        catch (Exception ex)        {

        {            // Placeholder for metrics reset functionality

            _logger.LogError(ex, "Failed to retrieve application info");            _logger.LogInformation("Metrics reset requested via API endpoint (ObservabilityService not yet integrated)");

            return StatusCode(500, new {             

                Status = "Error",             return Ok(new { 

                Message = "Failed to retrieve application info",                 message = "Metrics reset placeholder - full functionality available when ObservabilityService is integrated", 

                Timestamp = DateTime.UtcNow                 timestamp = DateTime.UtcNow 

            });            });

        }        }

    }        catch (Exception ex)

}        {
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
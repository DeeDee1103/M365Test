using Microsoft.Extensions.Diagnostics.HealthChecks;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EDiscoveryIntakeApi.Services;

/// <summary>
/// Comprehensive health check service for eDiscovery API
/// Provides detailed health information for monitoring and diagnostics
/// </summary>
public class EDiscoveryHealthService
{
    private readonly IDbContextFactory<EDiscoveryDbContext> _dbContextFactory;
    private readonly IAzureKeyVaultService _keyVaultService;
    private readonly ILogger<EDiscoveryHealthService> _logger;
    
    // Performance counters for health metrics
    private static readonly Dictionary<string, object> _healthMetrics = new();
    private static DateTime _lastMetricUpdate = DateTime.UtcNow;

    public EDiscoveryHealthService(
        IDbContextFactory<EDiscoveryDbContext> dbContextFactory,
        IAzureKeyVaultService keyVaultService,
        ILogger<EDiscoveryHealthService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = _dbContextFactory.CreateDbContext();
            var startTime = DateTime.UtcNow;
            
            // Test database connectivity and query performance
            var jobCount = await context.CollectionJobs.CountAsync(cancellationToken);
            var responseTime = DateTime.UtcNow - startTime;
            
            var data = new Dictionary<string, object>
            {
                { "TotalJobs", jobCount },
                { "ResponseTimeMs", responseTime.TotalMilliseconds },
                { "DatabaseProvider", "SQLite" },
                { "LastChecked", DateTime.UtcNow }
            };

            if (responseTime.TotalSeconds > 5)
            {
                return HealthCheckResult.Degraded("Database response time is slow", null, data);
            }

            return HealthCheckResult.Healthy("Database connection successful", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }

    public async Task<HealthCheckResult> CheckKeyVaultHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var isAvailable = await _keyVaultService.IsAvailableAsync(cancellationToken);
            var responseTime = DateTime.UtcNow - startTime;
            
            var data = new Dictionary<string, object>
            {
                { "IsAvailable", isAvailable },
                { "ResponseTimeMs", responseTime.TotalMilliseconds },
                { "LastChecked", DateTime.UtcNow }
            };

            if (!isAvailable)
            {
                return HealthCheckResult.Degraded("Key Vault not available - using local configuration", null, data);
            }

            if (responseTime.TotalSeconds > 3)
            {
                return HealthCheckResult.Degraded("Key Vault response time is slow", null, data);
            }

            return HealthCheckResult.Healthy("Key Vault connection successful", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key Vault health check failed");
            return HealthCheckResult.Degraded("Key Vault check failed - using local configuration", ex);
        }
    }

    public HealthCheckResult CheckApplicationHealth()
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                { "Version", "2.4.0" },
                { "Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production" },
                { "MachineName", Environment.MachineName },
                { "ProcessId", Environment.ProcessId },
                { "WorkingSetMB", GC.GetTotalMemory(false) / 1024 / 1024 },
                { "ThreadCount", System.Diagnostics.Process.GetCurrentProcess().Threads.Count },
                { "StartTime", System.Diagnostics.Process.GetCurrentProcess().StartTime },
                { "Uptime", DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime },
                { "LastChecked", DateTime.UtcNow }
            };

            return HealthCheckResult.Healthy("Application running normally", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application health check failed");
            return HealthCheckResult.Unhealthy("Application health check failed", ex);
        }
    }

    public static void UpdateMetric(string key, object value)
    {
        lock (_healthMetrics)
        {
            _healthMetrics[key] = value;
            _healthMetrics["LastUpdate"] = DateTime.UtcNow;
            _lastMetricUpdate = DateTime.UtcNow;
        }
    }

    public static Dictionary<string, object> GetMetrics()
    {
        lock (_healthMetrics)
        {
            var metrics = new Dictionary<string, object>(_healthMetrics);
            
            // Add system metrics
            var process = System.Diagnostics.Process.GetCurrentProcess();
            metrics["SystemMetrics"] = new
            {
                WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
                PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                TotalProcessorTime = process.TotalProcessorTime,
                UserProcessorTime = process.UserProcessorTime,
                StartTime = process.StartTime,
                Uptime = DateTime.UtcNow - process.StartTime
            };

            // Add GC metrics
            metrics["GCMetrics"] = new
            {
                TotalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };

            // Add last update timestamp
            metrics["MetricsCollectedAt"] = DateTime.UtcNow;
            metrics["MetricsAge"] = DateTime.UtcNow - _lastMetricUpdate;

            return metrics;
        }
    }

    public async Task<object> GetDetailedHealthAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new[]
        {
            CheckDatabaseHealthAsync(cancellationToken),
            CheckKeyVaultHealthAsync(cancellationToken)
        };

        var results = await Task.WhenAll(tasks);
        var appHealth = CheckApplicationHealth();

        var overallStatus = "Healthy";
        if (results.Any(r => r.Status == HealthStatus.Unhealthy) || appHealth.Status == HealthStatus.Unhealthy)
        {
            overallStatus = "Unhealthy";
        }
        else if (results.Any(r => r.Status == HealthStatus.Degraded) || appHealth.Status == HealthStatus.Degraded)
        {
            overallStatus = "Degraded";
        }

        return new
        {
            Status = overallStatus,
            Timestamp = DateTime.UtcNow,
            Checks = new
            {
                Application = new
                {
                    Status = appHealth.Status.ToString(),
                    Description = appHealth.Description,
                    Data = appHealth.Data
                },
                Database = new
                {
                    Status = results[0].Status.ToString(),
                    Description = results[0].Description,
                    Data = results[0].Data
                },
                KeyVault = new
                {
                    Status = results[1].Status.ToString(),
                    Description = results[1].Description,
                    Data = results[1].Data
                }
            },
            Metrics = GetMetrics()
        };
    }
}

/// <summary>
/// Database health check for ASP.NET Core health check middleware
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly EDiscoveryHealthService _healthService;

    public DatabaseHealthCheck(EDiscoveryHealthService healthService)
    {
        _healthService = healthService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await _healthService.CheckDatabaseHealthAsync(cancellationToken);
    }
}

/// <summary>
/// Key Vault health check for ASP.NET Core health check middleware
/// </summary>
public class KeyVaultHealthCheck : IHealthCheck
{
    private readonly EDiscoveryHealthService _healthService;

    public KeyVaultHealthCheck(EDiscoveryHealthService healthService)
    {
        _healthService = healthService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await _healthService.CheckKeyVaultHealthAsync(cancellationToken);
    }
}

/// <summary>
/// Application health check for ASP.NET Core health check middleware
/// </summary>
public class ApplicationHealthCheck : IHealthCheck
{
    private readonly EDiscoveryHealthService _healthService;

    public ApplicationHealthCheck(EDiscoveryHealthService healthService)
    {
        _healthService = healthService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_healthService.CheckApplicationHealth());
    }
}
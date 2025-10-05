using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Configuration;
using HybridGraphCollectorWorker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HybridGraphCollectorWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IComplianceLogger _complianceLogger;
    private readonly IGraphCollectorService _graphCollector;
    private readonly IAutoRouterService _autoRouter;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEDiscoveryApiClient _apiClient;
    private readonly IConfiguration _configuration;
    private readonly EDiscovery.Shared.Configuration.DeltaQueryOptions _deltaOptions;

    public Worker(
        ILogger<Worker> logger,
        IComplianceLogger complianceLogger,
        IGraphCollectorService graphCollector,
        IAutoRouterService autoRouter,
        IServiceProvider serviceProvider,
        IEDiscoveryApiClient apiClient,
        IConfiguration configuration,
        Microsoft.Extensions.Options.IOptions<EDiscovery.Shared.Configuration.DeltaQueryOptions> deltaOptions)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;
        _graphCollector = graphCollector;
        _autoRouter = autoRouter;
        _serviceProvider = serviceProvider;
        _apiClient = apiClient;
        _configuration = configuration;
        _deltaOptions = deltaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        _logger.LogInformation("Hybrid Graph Collector Worker started at: {Time} | CorrelationId: {CorrelationId}", 
            DateTimeOffset.Now, correlationId);
        
        _complianceLogger.LogAudit("WorkerServiceExecutionStarted", new { StartTime = DateTimeOffset.Now }, null, correlationId);

        // For POC, we'll simulate processing jobs
        // In production, this would poll for pending jobs from the API or use a message queue
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker execution cancelled");
                break;
            }
            catch (Exception ex)
            {
                _complianceLogger.LogError(ex, "Worker.ExecuteAsync", null, correlationId);
            }

            // Wait before checking for more jobs
            var pollInterval = _configuration.GetValue<int>("Worker:PollIntervalSeconds", 30);
            _logger.LogDebug("Waiting {PollInterval} seconds before next poll", pollInterval);
            await Task.Delay(TimeSpan.FromSeconds(pollInterval), stoppingToken);
        }

        _logger.LogInformation("Hybrid Graph Collector Worker stopped at: {Time} | CorrelationId: {CorrelationId}", 
            DateTimeOffset.Now, correlationId);
        
        _complianceLogger.LogAudit("WorkerServiceExecutionStopped", new { StopTime = DateTimeOffset.Now }, null, correlationId);
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("Worker.ProcessPendingJobs", correlationId);
        
        _logger.LogDebug("Checking for pending collection jobs | CorrelationId: {CorrelationId}", correlationId);
        // For POC, simulate a sample collection job
        // In production, this would query the API for pending jobs
        
        if (_configuration.GetValue<bool>("Worker:EnableSampleJob", false))
        {
            await ProcessSampleJobAsync(cancellationToken);
        }

        // Process delta queries if enabled and background processing is configured
        if (_deltaOptions.BackgroundDeltaQueries)
        {
            await ProcessDeltaQueriesAsync(cancellationToken);
        }
    }

    private async Task ProcessDeltaQueriesAsync(CancellationToken cancellationToken)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("Worker.ProcessDeltaQueries", correlationId);
        
        _logger.LogDebug("Processing delta queries | CorrelationId: {CorrelationId}", correlationId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var deltaQueryService = scope.ServiceProvider.GetRequiredService<IDeltaQueryService>();
            
            // Get all active delta cursors
            var activeCursors = await deltaQueryService.GetActiveDeltaCursorsAsync();
            
            if (!activeCursors.Any())
            {
                _logger.LogDebug("No active delta cursors found | CorrelationId: {CorrelationId}", correlationId);
                return;
            }

            _logger.LogInformation("Processing {CursorCount} active delta cursors | CorrelationId: {CorrelationId}", 
                activeCursors.Count, correlationId);

            foreach (var cursor in activeCursors)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessSingleDeltaCursorAsync(cursor, correlationId, cancellationToken);
            }

            // Periodic cleanup of stale cursors
            if (_deltaOptions.EnableAutomaticCleanup)
            {
                await deltaQueryService.CleanupStaleDeltaCursorsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing delta queries | CorrelationId: {CorrelationId}", correlationId);
        }
    }

    private async Task ProcessSingleDeltaCursorAsync(DeltaCursor cursor, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            // Check if enough time has passed since last delta query
            var timeSinceLastQuery = DateTime.UtcNow - cursor.LastDeltaTime;
            var intervalMinutes = TimeSpan.FromMinutes(_deltaOptions.DeltaQueryIntervalMinutes);
            
            if (timeSinceLastQuery < intervalMinutes)
            {
                _logger.LogDebug("Skipping delta query for {ScopeId} - interval not reached (last: {LastQuery}) | CorrelationId: {CorrelationId}", 
                    cursor.ScopeId, cursor.LastDeltaTime, correlationId);
                return;
            }

            _logger.LogInformation("Executing delta query for {ScopeId} ({DeltaType}) | CorrelationId: {CorrelationId}", 
                cursor.ScopeId, cursor.DeltaType, correlationId);

            using var scope = _serviceProvider.CreateScope();
            var deltaQueryService = scope.ServiceProvider.GetRequiredService<IDeltaQueryService>();

            // Execute the appropriate delta query based on type
            DeltaQueryResult? result = cursor.DeltaType switch
            {
                DeltaType.Mail => await deltaQueryService.QueryMailDeltaAsync(cursor.CustodianEmail, cursor, cancellationToken),
                DeltaType.OneDrive => await deltaQueryService.QueryOneDriveDeltaAsync(cursor.CustodianEmail, cursor, cancellationToken),
                _ => null
            };

            if (result == null)
            {
                _logger.LogWarning("Unsupported delta type: {DeltaType} for {ScopeId} | CorrelationId: {CorrelationId}", 
                    cursor.DeltaType, cursor.ScopeId, correlationId);
                return;
            }

            if (result.IsSuccessful)
            {
                // Update cursor with new delta token
                if (!string.IsNullOrEmpty(result.NextDeltaToken))
                {
                    await deltaQueryService.UpdateDeltaCursorAsync(cursor.ScopeId, result.NextDeltaToken, result.ItemCount, result.TotalSizeBytes);
                }

                // Log collection results to API if items were found
                if (result.ItemCount > 0)
                {
                    await _apiClient.LogJobEventAsync(cursor.CollectionJobId ?? 0, EDiscovery.Shared.Models.LogLevel.Information, "DeltaQuery",
                        $"Delta collection completed for {cursor.DeltaType}: {result.ItemCount} items, {result.TotalSizeBytes:N0} bytes",
                        $"Scope: {cursor.ScopeId}, Duration: {result.QueryDuration.TotalMilliseconds:F1}ms");

                    _logger.LogInformation("Delta query completed for {ScopeId}: {ItemCount} items, {SizeBytes} bytes in {Duration}ms | CorrelationId: {CorrelationId}",
                        cursor.ScopeId, result.ItemCount, result.TotalSizeBytes, result.QueryDuration.TotalMilliseconds, correlationId);
                }
                else
                {
                    _logger.LogDebug("No new items found in delta query for {ScopeId} | CorrelationId: {CorrelationId}", 
                        cursor.ScopeId, correlationId);
                }
            }
            else
            {
                _logger.LogError("Delta query failed for {ScopeId}: {ErrorMessage} | CorrelationId: {CorrelationId}", 
                    cursor.ScopeId, result.ErrorMessage, correlationId);
                
                await _apiClient.LogJobEventAsync(cursor.CollectionJobId ?? 0, EDiscovery.Shared.Models.LogLevel.Error, "DeltaQuery",
                    $"Delta query failed for {cursor.DeltaType}: {result.ErrorMessage}",
                    $"Scope: {cursor.ScopeId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing delta cursor {ScopeId} | CorrelationId: {CorrelationId}", cursor.ScopeId, correlationId);
        }
    }

    private async Task ProcessSampleJobAsync(CancellationToken cancellationToken)
    {
        var sampleCustodian = _configuration["Worker:SampleCustodian"] ?? "user@company.com";
        
        _logger.LogInformation("Processing sample collection job for custodian: {CustodianEmail}", sampleCustodian);

        // Create a sample collection request
        var request = new CollectionRequest
        {
            CustodianEmail = sampleCustodian,
            JobType = CollectionJobType.Email,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow,
            Keywords = new List<string> { "contract", "agreement" },
            IncludeAttachments = true,
            OutputPath = "./output"
        };

        // Use AutoRouter to determine the best collection route
        var routerDecision = await _autoRouter.DetermineOptimalRouteAsync(request);
        
        await _apiClient.LogJobEventAsync(0, EDiscovery.Shared.Models.LogLevel.Information, "AutoRouter", 
            $"Route decision: {routerDecision.RecommendedRoute}", 
            $"Reason: {routerDecision.Reason}. Confidence: {routerDecision.ConfidenceScore:P1}");

        // Execute collection based on route decision
        CollectionResult result;
        
        switch (routerDecision.RecommendedRoute)
        {
            case CollectionRoute.GraphApi:
                result = await ExecuteGraphApiCollection(request, cancellationToken);
                break;
            
            case CollectionRoute.GraphDataConnect:
                result = await ExecuteGraphDataConnectCollection(request, cancellationToken);
                break;
            
            default:
                _logger.LogWarning("Unknown collection route: {Route}", routerDecision.RecommendedRoute);
                return;
        }

        // Log collection results
        if (result.IsSuccessful)
        {
            _logger.LogInformation("Collection completed successfully. Items: {ItemCount}, Size: {SizeBytes} bytes", 
                result.TotalItemCount, result.TotalSizeBytes);
        }
        else
        {
            _logger.LogError("Collection failed: {ErrorMessage}", result.ErrorMessage);
        }

        // Disable sample job after running once
        _configuration["Worker:EnableSampleJob"] = "false";
    }

    private async Task<CollectionResult> ExecuteGraphApiCollection(CollectionRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Graph API collection for job type: {JobType}", request.JobType);

        return request.JobType switch
        {
            CollectionJobType.Email => await _graphCollector.CollectEmailAsync(request, cancellationToken),
            CollectionJobType.OneDrive => await _graphCollector.CollectOneDriveAsync(request, cancellationToken),
            CollectionJobType.SharePoint => await _graphCollector.CollectSharePointAsync(request, cancellationToken),
            CollectionJobType.Teams => await _graphCollector.CollectTeamsAsync(request, cancellationToken),
            _ => new CollectionResult { ErrorMessage = $"Unsupported job type: {request.JobType}" }
        };
    }

    private async Task<CollectionResult> ExecuteGraphDataConnectCollection(CollectionRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graph Data Connect collection requested for job type: {JobType}", request.JobType);
        
        // For POC, Graph Data Connect is not implemented
        // In production, this would trigger Azure Data Factory pipelines
        
        await Task.Delay(100, cancellationToken); // Simulate processing
        
        return new CollectionResult
        {
            IsSuccessful = false,
            ErrorMessage = "Graph Data Connect collection not implemented in POC"
        };
    }
}

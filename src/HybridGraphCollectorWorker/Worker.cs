using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using HybridGraphCollectorWorker.Services;

namespace HybridGraphCollectorWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IGraphCollectorService _graphCollector;
    private readonly IAutoRouterService _autoRouter;
    private readonly IEDiscoveryApiClient _apiClient;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger,
        IGraphCollectorService graphCollector,
        IAutoRouterService autoRouter,
        IEDiscoveryApiClient apiClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _graphCollector = graphCollector;
        _autoRouter = autoRouter;
        _apiClient = apiClient;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hybrid Graph Collector Worker started at: {Time}", DateTimeOffset.Now);

        // For POC, we'll simulate processing jobs
        // In production, this would poll for pending jobs from the API or use a message queue
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker execution loop");
            }

            // Wait before checking for more jobs
            var pollInterval = _configuration.GetValue<int>("Worker:PollIntervalSeconds", 30);
            await Task.Delay(TimeSpan.FromSeconds(pollInterval), stoppingToken);
        }

        _logger.LogInformation("Hybrid Graph Collector Worker stopped at: {Time}", DateTimeOffset.Now);
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        // For POC, simulate a sample collection job
        // In production, this would query the API for pending jobs
        
        if (_configuration.GetValue<bool>("Worker:EnableSampleJob", false))
        {
            await ProcessSampleJobAsync(cancellationToken);
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

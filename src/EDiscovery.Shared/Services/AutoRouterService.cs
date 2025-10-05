using EDiscovery.Shared.Models;
using Microsoft.Extensions.Logging;

namespace EDiscovery.Shared.Services;

public interface IAutoRouterService
{
    Task<AutoRouterDecision> DetermineOptimalRouteAsync(CollectionRequest request);
    Task<CollectionQuota> GetCurrentQuotaAsync();
}

public class AutoRouterService : IAutoRouterService
{
    private readonly ILogger<AutoRouterService> _logger;
    private readonly IComplianceLogger _complianceLogger;
    
    // Thresholds for routing decisions
    private const long GRAPH_API_SIZE_LIMIT = 100L * 1024 * 1024 * 1024; // 100GB
    private const int GRAPH_API_ITEM_LIMIT = 500_000; // 500k items
    
    public AutoRouterService(ILogger<AutoRouterService> logger, IComplianceLogger complianceLogger)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;
    }
    
    public async Task<AutoRouterDecision> DetermineOptimalRouteAsync(CollectionRequest request)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("AutoRouter.DetermineOptimalRoute", correlationId);
        
        _logger.LogInformation("Determining optimal route for custodian: {CustodianEmail} | CorrelationId: {CorrelationId}", 
            request.CustodianEmail, correlationId);
        
        _complianceLogger.LogAudit("AutoRouterDecisionStarted", new 
        { 
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType.ToString(),
            DateRange = new { request.StartDate, request.EndDate }
        }, request.CustodianEmail, correlationId);

        try
        {
            // Get current quota usage
            var quota = await GetCurrentQuotaAsync();
            
            // Estimate collection size and item count
            var (estimatedSize, estimatedItems) = await EstimateCollectionSizeAsync(request);
            
            _logger.LogInformation("Collection estimates - Size: {EstimatedSize} bytes ({EstimatedSizeMB:F2} MB), Items: {EstimatedItems} | CorrelationId: {CorrelationId}",
                estimatedSize, estimatedSize / 1024.0 / 1024.0, estimatedItems, correlationId);

            var decision = new AutoRouterDecision
            {
                EstimatedDataSizeBytes = estimatedSize,
                EstimatedItemCount = estimatedItems,
                Metrics = new Dictionary<string, object>
                {
                    ["quota_used_bytes"] = quota.UsedBytes,
                    ["quota_limit_bytes"] = quota.LimitBytes,
                    ["quota_used_items"] = quota.UsedItems,
                    ["quota_limit_items"] = quota.LimitItems,
                    ["correlation_id"] = correlationId
                }
            };
            
            // Apply routing logic with detailed logging
            if (quota.UsedBytes < GRAPH_API_SIZE_LIMIT && quota.UsedItems < GRAPH_API_ITEM_LIMIT)
            {
                if (estimatedSize + quota.UsedBytes < GRAPH_API_SIZE_LIMIT && 
                    estimatedItems + quota.UsedItems < GRAPH_API_ITEM_LIMIT)
                {
                    decision.RecommendedRoute = CollectionRoute.GraphApi;
                    decision.Reason = "Collection fits within Graph API limits";
                    decision.ConfidenceScore = 0.9;
                    
                    _logger.LogInformation("Route decision: Graph API selected - fits within limits | CorrelationId: {CorrelationId}", correlationId);
                }
                else
                {
                    decision.RecommendedRoute = CollectionRoute.GraphDataConnect;
                    decision.Reason = "Collection would exceed Graph API limits";
                    decision.ConfidenceScore = 0.8;
                    
                    _logger.LogInformation("Route decision: Graph Data Connect selected - would exceed Graph API limits | CorrelationId: {CorrelationId}", correlationId);
                }
            }
            else
            {
                decision.RecommendedRoute = CollectionRoute.GraphDataConnect;
                decision.Reason = "Current quota usage exceeds Graph API thresholds";
                decision.ConfidenceScore = 0.95;
                
                _logger.LogInformation("Route decision: Graph Data Connect selected - current quota already exceeded | CorrelationId: {CorrelationId}", correlationId);
            }

            _logger.LogInformation("Route decision completed: {Route} (Confidence: {Confidence:P1}) | CorrelationId: {CorrelationId}", 
                decision.RecommendedRoute, decision.ConfidenceScore, correlationId);
            
            // Log compliance audit
            _complianceLogger.LogAudit("AutoRouterDecisionCompleted", new
            {
                RecommendedRoute = decision.RecommendedRoute.ToString(),
                Reason = decision.Reason,
                ConfidenceScore = decision.ConfidenceScore,
                EstimatedSize = estimatedSize,
                EstimatedItems = estimatedItems,
                QuotaUsage = new { quota.UsedBytes, quota.UsedItems }
            }, request.CustodianEmail, correlationId);
            
            return decision;
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "AutoRouter.DetermineOptimalRoute", new 
            { 
                CustodianEmail = request.CustodianEmail,
                JobType = request.JobType.ToString()
            }, correlationId);
            throw;
        }
    }
    
    public async Task<CollectionQuota> GetCurrentQuotaAsync()
    {
        // In POC, return mock data
        // In production, this would query actual Graph API quotas
        await Task.Delay(10); // Simulate async call
        
        return new CollectionQuota
        {
            UsedBytes = 50L * 1024 * 1024 * 1024, // 50GB used
            LimitBytes = GRAPH_API_SIZE_LIMIT,
            UsedItems = 250_000, // 250k items used
            LimitItems = GRAPH_API_ITEM_LIMIT,
            LastUpdated = DateTime.UtcNow
        };
    }
    
    private async Task<(long sizeBytes, int itemCount)> EstimateCollectionSizeAsync(CollectionRequest request)
    {
        // In POC, return estimates based on job type
        // In production, this would use Graph API to get actual estimates
        await Task.Delay(10); // Simulate async call
        
        return request.JobType switch
        {
            CollectionJobType.Email => (5L * 1024 * 1024 * 1024, 10_000), // 5GB, 10k items
            CollectionJobType.OneDrive => (10L * 1024 * 1024 * 1024, 5_000), // 10GB, 5k items
            CollectionJobType.SharePoint => (20L * 1024 * 1024 * 1024, 15_000), // 20GB, 15k items
            CollectionJobType.Teams => (2L * 1024 * 1024 * 1024, 20_000), // 2GB, 20k items
            CollectionJobType.Mixed => (25L * 1024 * 1024 * 1024, 30_000), // 25GB, 30k items
            _ => (1L * 1024 * 1024 * 1024, 1_000) // 1GB, 1k items default
        };
    }
}
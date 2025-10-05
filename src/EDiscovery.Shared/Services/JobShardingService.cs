using EDiscovery.Shared.Models;
using EDiscoveryIntakeApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Implementation of job sharding service for large collection jobs
/// </summary>
public class JobShardingService : IJobShardingService
{
    private readonly IDbContextFactory<EDiscoveryDbContext> _contextFactory;
    private readonly ILogger<JobShardingService> _logger;
    private readonly IAutoRouterService _autoRouter;

    public JobShardingService(
        IDbContextFactory<EDiscoveryDbContext> contextFactory,
        ILogger<JobShardingService> logger,
        IAutoRouterService autoRouter)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _autoRouter = autoRouter;
    }

    public async Task<ShardedJobResponse> CreateShardedJobAsync(CreateShardedJobRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating sharded job for {CustodianCount} custodians from {StartDate} to {EndDate}", 
            request.CustodianEmails.Count, request.StartDate, request.EndDate);

        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate matter exists
        var matter = await context.Matters.FindAsync(request.MatterId);
        if (matter == null)
        {
            throw new ArgumentException($"Matter {request.MatterId} not found");
        }

        // Use default sharding config if not provided
        var config = request.ShardingConfig ?? new JobShardingConfig
        {
            MaxDateWindowSize = TimeSpan.FromDays(30),
            MaxShardsPerCustodian = 12,
            MaxShardSizeBytes = 50L * 1024 * 1024 * 1024, // 50GB
            MaxShardItemCount = 250000
        };

        // Create parent job
        var parentJob = new CollectionJob
        {
            MatterId = request.MatterId,
            CustodianEmail = string.Join(";", request.CustodianEmails), // Multi-custodian indicator
            JobType = request.JobType,
            Status = CollectionJobStatus.Pending,
            Route = CollectionRoute.GraphApi, // Will be determined per shard
            CreatedDate = DateTime.UtcNow,
            Priority = request.Priority,
            OutputPath = request.OutputPath
        };

        context.CollectionJobs.Add(parentJob);
        await context.SaveChangesAsync(cancellationToken);

        var createdShards = new List<JobShard>();
        var totalShardCount = 0;

        // Create shards for each custodian
        foreach (var custodianEmail in request.CustodianEmails)
        {
            var custodianShards = await CreateCustodianShardsAsync(
                context, parentJob.Id, custodianEmail, request, config, cancellationToken);
            
            createdShards.AddRange(custodianShards);
            totalShardCount += custodianShards.Count;
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created {ShardCount} shards for parent job {JobId}", totalShardCount, parentJob.Id);

        return new ShardedJobResponse
        {
            ParentJobId = parentJob.Id,
            CreatedShards = createdShards,
            TotalShards = totalShardCount,
            EstimatedDuration = TimeSpan.FromHours(totalShardCount * 2), // Rough estimate
            ShardingStrategy = $"Date windows of {config.MaxDateWindowSize.TotalDays} days per custodian",
            ShardingMetrics = new Dictionary<string, object>
            {
                ["CustodianCount"] = request.CustodianEmails.Count,
                ["AverageShardsPerCustodian"] = (double)totalShardCount / request.CustodianEmails.Count,
                ["DateRangeDays"] = (request.EndDate - request.StartDate).TotalDays,
                ["MaxShardSizeGB"] = config.MaxShardSizeBytes / (1024.0 * 1024 * 1024),
                ["MaxItemsPerShard"] = config.MaxShardItemCount
            }
        };
    }

    private async Task<List<JobShard>> CreateCustodianShardsAsync(
        EDiscoveryDbContext context, 
        int parentJobId, 
        string custodianEmail, 
        CreateShardedJobRequest request, 
        JobShardingConfig config,
        CancellationToken cancellationToken)
    {
        var shards = new List<JobShard>();
        var currentDate = request.StartDate;
        var endDate = request.EndDate;
        var shardIndex = 0;

        while (currentDate < endDate)
        {
            var shardEndDate = currentDate.Add(config.MaxDateWindowSize);
            if (shardEndDate > endDate)
                shardEndDate = endDate;

            // Skip if the window is too small
            if ((shardEndDate - currentDate).TotalDays < config.MinimumShardDays)
                break;

            // Get route decision for this shard
            var routeRequest = new CollectionRequest
            {
                CustodianEmail = custodianEmail,
                JobType = request.JobType,
                StartDate = currentDate,
                EndDate = shardEndDate,
                Keywords = request.Keywords ?? new List<string>(),
                IncludeAttachments = request.IncludeAttachments,
                OutputPath = request.OutputPath ?? string.Empty
            };

            var routeDecision = await _autoRouter.DetermineOptimalRouteAsync(routeRequest);

            var shard = new JobShard
            {
                ParentJobId = parentJobId,
                CustodianEmail = custodianEmail,
                StartDate = currentDate,
                EndDate = shardEndDate,
                JobType = request.JobType,
                Status = JobShardStatus.Pending,
                Route = routeDecision.RecommendedRoute,
                ShardIndex = shardIndex,
                ShardIdentifier = GenerateShardIdentifier(custodianEmail, currentDate, shardEndDate),
                EstimatedDataSizeBytes = routeDecision.EstimatedDataSizeBytes,
                EstimatedItemCount = routeDecision.EstimatedItemCount,
                CreatedDate = DateTime.UtcNow,
                OutputPath = request.OutputPath,
                MaxRetries = 3
            };

            shards.Add(shard);
            context.JobShards.Add(shard);

            currentDate = shardEndDate;
            shardIndex++;

            // Safety check to prevent infinite loops
            if (shardIndex >= config.MaxShardsPerCustodian)
            {
                _logger.LogWarning("Reached maximum shard count {MaxShards} for custodian {Custodian}", 
                    config.MaxShardsPerCustodian, custodianEmail);
                break;
            }
        }

        // Update total shard count for all shards
        foreach (var shard in shards)
        {
            shard.TotalShards = shards.Count;
        }

        _logger.LogInformation("Created {ShardCount} shards for custodian {Custodian}", shards.Count, custodianEmail);
        return shards;
    }

    private static string GenerateShardIdentifier(string custodianEmail, DateTime startDate, DateTime endDate)
    {
        var emailPrefix = custodianEmail.Split('@')[0];
        var startStr = startDate.ToString("yyyyMMdd");
        var endStr = endDate.ToString("yyyyMMdd");
        return $"{emailPrefix}_{startStr}_{endStr}";
    }

    public async Task<JobShard?> GetNextAvailableShardAsync(string workerId, int userId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var availableShard = await context.JobShards
            .Where(s => s.Status == JobShardStatus.Pending && 
                       (s.LockExpiry == null || s.LockExpiry < DateTime.UtcNow))
            .OrderBy(s => s.CreatedDate)
            .ThenBy(s => s.ShardIndex)
            .FirstOrDefaultAsync(cancellationToken);

        return availableShard;
    }

    public async Task<bool> AcquireShardLockAsync(int shardId, string workerId, int userId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var lockToken = Guid.NewGuid().ToString();
        var lockExpiry = DateTime.UtcNow.AddMinutes(30);

        var rowsAffected = await context.Database.ExecuteSqlRawAsync(
            @"UPDATE JobShards 
              SET AssignedUserId = {0}, AssignedWorkerId = {1}, AssignedAt = {2}, 
                  LockToken = {3}, LockExpiry = {4}, Status = {5}
              WHERE Id = {6} AND (LockExpiry IS NULL OR LockExpiry < {7})",
            userId, workerId, DateTime.UtcNow, lockToken, lockExpiry, 
            (int)JobShardStatus.Assigned, shardId, DateTime.UtcNow,
            cancellationToken);

        var lockAcquired = rowsAffected > 0;
        
        if (lockAcquired)
        {
            _logger.LogInformation("Acquired lock on shard {ShardId} for worker {WorkerId}", shardId, workerId);
        }

        return lockAcquired;
    }

    public async Task<bool> ReleaseShardLockAsync(int shardId, string workerId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rowsAffected = await context.Database.ExecuteSqlRawAsync(
            @"UPDATE JobShards 
              SET AssignedUserId = NULL, AssignedWorkerId = NULL, AssignedAt = NULL, 
                  LockToken = NULL, LockExpiry = NULL, Status = {0}
              WHERE Id = {1} AND AssignedWorkerId = {2}",
            (int)JobShardStatus.Pending, shardId, workerId,
            cancellationToken);

        var lockReleased = rowsAffected > 0;
        
        if (lockReleased)
        {
            _logger.LogInformation("Released lock on shard {ShardId} from worker {WorkerId}", shardId, workerId);
        }

        return lockReleased;
    }

    public async Task<JobShardCheckpoint> CreateCheckpointAsync(int shardId, string checkpointType, string checkpointKey, 
        string checkpointData, string correlationId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var checkpoint = new JobShardCheckpoint
        {
            JobShardId = shardId,
            CheckpointType = checkpointType,
            CheckpointKey = checkpointKey,
            CheckpointData = checkpointData,
            CorrelationId = correlationId,
            CreatedDate = DateTime.UtcNow,
            IsCompleted = false
        };

        context.JobShardCheckpoints.Add(checkpoint);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created checkpoint {CheckpointId} for shard {ShardId} | Type: {Type} | Key: {Key}", 
            checkpoint.Id, shardId, checkpointType, checkpointKey);

        return checkpoint;
    }

    public async Task<bool> CompleteCheckpointAsync(int checkpointId, int itemsProcessed, long bytesProcessed, 
        CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rowsAffected = await context.Database.ExecuteSqlRawAsync(
            @"UPDATE JobShardCheckpoints 
              SET IsCompleted = 1, CompletedDate = {0}, ItemsProcessed = {1}, BytesProcessed = {2}
              WHERE Id = {3}",
            DateTime.UtcNow, itemsProcessed, bytesProcessed, checkpointId,
            cancellationToken);

        var completed = rowsAffected > 0;
        
        if (completed)
        {
            _logger.LogDebug("Completed checkpoint {CheckpointId} with {Items} items and {Bytes} bytes", 
                checkpointId, itemsProcessed, bytesProcessed);
        }

        return completed;
    }

    public async Task<List<JobShardCheckpoint>> GetShardCheckpointsAsync(int shardId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.JobShardCheckpoints
            .Where(c => c.JobShardId == shardId)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<JobShardCheckpoint>> GetIncompleteCheckpointsAsync(int shardId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.JobShardCheckpoints
            .Where(c => c.JobShardId == shardId && !c.IsCompleted)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateShardProgressAsync(int shardId, int itemsProcessed, long bytesProcessed, 
        CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var shard = await context.JobShards.FindAsync(shardId);
        if (shard == null) return false;

        shard.ProcessedItemCount = itemsProcessed;
        shard.ProcessedDataSizeBytes = bytesProcessed;
        
        // Calculate progress percentage
        if (shard.EstimatedItemCount > 0)
        {
            shard.ProgressPercentage = Math.Min(100.0, (double)itemsProcessed / shard.EstimatedItemCount * 100);
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteShardAsync(int shardId, bool isSuccessful, int actualItemCount, long actualDataSizeBytes, 
        string? manifestHash = null, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var shard = await context.JobShards.FindAsync(shardId);
        if (shard == null) return false;

        shard.Status = isSuccessful ? JobShardStatus.Completed : JobShardStatus.Failed;
        shard.EndTime = DateTime.UtcNow;
        shard.ActualItemCount = actualItemCount;
        shard.ActualDataSizeBytes = actualDataSizeBytes;
        shard.ManifestHash = manifestHash;
        shard.ErrorMessage = errorMessage;
        shard.ProgressPercentage = isSuccessful ? 100.0 : shard.ProgressPercentage;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Completed shard {ShardId} | Success: {Success} | Items: {Items} | Size: {Size} bytes", 
            shardId, isSuccessful, actualItemCount, actualDataSizeBytes);

        return true;
    }

    public async Task<List<JobShard>> GetJobShardsAsync(int parentJobId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.JobShards
            .Where(s => s.ParentJobId == parentJobId)
            .Include(s => s.Checkpoints)
            .OrderBy(s => s.ShardIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobShardProgress> GetJobProgressAsync(int parentJobId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var shards = await context.JobShards
            .Where(s => s.ParentJobId == parentJobId)
            .ToListAsync(cancellationToken);

        var totalShards = shards.Count;
        var completedShards = shards.Count(s => s.Status == JobShardStatus.Completed);
        var failedShards = shards.Count(s => s.Status == JobShardStatus.Failed);
        var processingShards = shards.Count(s => s.Status == JobShardStatus.Processing || s.Status == JobShardStatus.Running);
        var pendingShards = shards.Count(s => s.Status == JobShardStatus.Pending || s.Status == JobShardStatus.Assigned);

        var totalItemsProcessed = shards.Sum(s => s.ProcessedItemCount);
        var totalBytesProcessed = shards.Sum(s => s.ProcessedDataSizeBytes);
        var estimatedTotalItems = shards.Sum(s => s.EstimatedItemCount);
        var estimatedTotalBytes = shards.Sum(s => s.EstimatedDataSizeBytes);

        var overallProgress = totalShards > 0 ? shards.Average(s => s.ProgressPercentage) : 0.0;

        return new JobShardProgress
        {
            ParentJobId = parentJobId,
            TotalShards = totalShards,
            CompletedShards = completedShards,
            FailedShards = failedShards,
            ProcessingShards = processingShards,
            PendingShards = pendingShards,
            OverallProgressPercentage = overallProgress,
            TotalItemsProcessed = totalItemsProcessed,
            TotalBytesProcessed = totalBytesProcessed,
            EstimatedTotalItems = estimatedTotalItems,
            EstimatedTotalBytes = estimatedTotalBytes,
            ShardStatusCounts = shards.GroupBy(s => s.Status.ToString())
                                   .ToDictionary(g => g.Key, g => g.Count()),
            ActiveShards = shards.Where(s => s.Status == JobShardStatus.Processing || s.Status == JobShardStatus.Running).ToList()
        };
    }

    public async Task<bool> RetryShardAsync(int shardId, string errorMessage, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var shard = await context.JobShards.FindAsync(shardId);
        if (shard == null) return false;

        if (shard.RetryCount >= shard.MaxRetries)
        {
            _logger.LogWarning("Shard {ShardId} has exceeded maximum retries ({MaxRetries})", shardId, shard.MaxRetries);
            return false;
        }

        shard.RetryCount++;
        shard.LastRetryTime = DateTime.UtcNow;
        shard.Status = JobShardStatus.Retrying;
        shard.ErrorMessage = errorMessage;
        
        // Clear assignment for retry
        shard.AssignedUserId = null;
        shard.AssignedWorkerId = null;
        shard.AssignedAt = null;
        shard.LockToken = null;
        shard.LockExpiry = null;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scheduled retry {RetryCount}/{MaxRetries} for shard {ShardId}", 
            shard.RetryCount, shard.MaxRetries, shardId);

        return true;
    }

    public async Task<ShardingRecommendation> EvaluateShardingNeedAsync(CreateShardedJobRequest request, 
        CancellationToken cancellationToken = default)
    {
        var dateRangeDays = (request.EndDate - request.StartDate).TotalDays;
        var custodianCount = request.CustodianEmails.Count;
        var totalCustodianDays = dateRangeDays * custodianCount;

        var shouldShard = totalCustodianDays > 365 || // More than 1 year total
                         custodianCount > 5 || // More than 5 custodians
                         dateRangeDays > 90; // More than 3 months

        var recommendedShardCount = Math.Max(1, (int)Math.Ceiling(totalCustodianDays / 30)); // 30-day windows
        var recommendedWindowSize = TimeSpan.FromDays(Math.Min(30, dateRangeDays / Math.Max(1, recommendedShardCount / custodianCount)));

        var factors = new List<string>();
        if (dateRangeDays > 90) factors.Add($"Long date range ({dateRangeDays:F0} days)");
        if (custodianCount > 5) factors.Add($"Many custodians ({custodianCount})");
        if (totalCustodianDays > 365) factors.Add($"Large total scope ({totalCustodianDays:F0} custodian-days)");

        return new ShardingRecommendation
        {
            ShouldShard = shouldShard,
            Reason = shouldShard 
                ? $"Job should be sharded due to: {string.Join(", ", factors)}"
                : "Job is small enough to process without sharding",
            RecommendedShardCount = recommendedShardCount,
            RecommendedDateWindowSize = recommendedWindowSize,
            ShardingFactors = factors,
            Metrics = new Dictionary<string, object>
            {
                ["DateRangeDays"] = dateRangeDays,
                ["CustodianCount"] = custodianCount,
                ["TotalCustodianDays"] = totalCustodianDays,
                ["RecommendedShardsPerCustodian"] = recommendedShardCount / custodianCount
            }
        };
    }

    public async Task<int> CleanupExpiredShardLocksAsync(CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var expiredCount = await context.Database.ExecuteSqlRawAsync(
            @"UPDATE JobShards 
              SET AssignedUserId = NULL, AssignedWorkerId = NULL, AssignedAt = NULL, 
                  LockToken = NULL, LockExpiry = NULL, Status = {0}
              WHERE LockExpiry IS NOT NULL AND LockExpiry < {1} AND Status = {2}",
            (int)JobShardStatus.Pending, DateTime.UtcNow, (int)JobShardStatus.Assigned,
            cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired shard locks", expiredCount);
        }

        return expiredCount;
    }
}
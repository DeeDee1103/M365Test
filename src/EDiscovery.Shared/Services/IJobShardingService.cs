using EDiscovery.Shared.Models;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Service for sharding large collection jobs by custodian and date windows
/// </summary>
public interface IJobShardingService
{
    /// <summary>
    /// Creates shards for a large collection job based on custodians and date ranges
    /// </summary>
    /// <param name="request">Sharded job creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sharded job response with created shards</returns>
    Task<ShardedJobResponse> CreateShardedJobAsync(CreateShardedJobRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the next available job shard for processing
    /// </summary>
    /// <param name="workerId">Worker identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next available job shard or null if none available</returns>
    Task<JobShard?> GetNextAvailableShardAsync(string workerId, int userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Acquires a lock on a specific job shard
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="workerId">Worker identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was acquired successfully</returns>
    Task<bool> AcquireShardLockAsync(int shardId, string workerId, int userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Releases a lock on a job shard
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="workerId">Worker identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was released successfully</returns>
    Task<bool> ReleaseShardLockAsync(int shardId, string workerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a checkpoint for a job shard to enable idempotent restarts
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="checkpointType">Type of checkpoint</param>
    /// <param name="checkpointKey">Unique key for this checkpoint</param>
    /// <param name="checkpointData">JSON data for restart context</param>
    /// <param name="correlationId">Correlation identifier for tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created checkpoint</returns>
    Task<JobShardCheckpoint> CreateCheckpointAsync(int shardId, string checkpointType, string checkpointKey, 
        string checkpointData, string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a checkpoint as completed
    /// </summary>
    /// <param name="checkpointId">Checkpoint identifier</param>
    /// <param name="itemsProcessed">Number of items processed</param>
    /// <param name="bytesProcessed">Number of bytes processed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if checkpoint was marked as completed</returns>
    Task<bool> CompleteCheckpointAsync(int checkpointId, int itemsProcessed, long bytesProcessed, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all checkpoints for a job shard for restart recovery
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of checkpoints for the shard</returns>
    Task<List<JobShardCheckpoint>> GetShardCheckpointsAsync(int shardId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets incomplete checkpoints for a job shard to resume processing
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of incomplete checkpoints</returns>
    Task<List<JobShardCheckpoint>> GetIncompleteCheckpointsAsync(int shardId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates progress for a job shard
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="itemsProcessed">Items processed so far</param>
    /// <param name="bytesProcessed">Bytes processed so far</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if progress was updated</returns>
    Task<bool> UpdateShardProgressAsync(int shardId, int itemsProcessed, long bytesProcessed, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Completes a job shard with final results
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="isSuccessful">Whether the shard completed successfully</param>
    /// <param name="actualItemCount">Actual number of items collected</param>
    /// <param name="actualDataSizeBytes">Actual size of data collected</param>
    /// <param name="manifestHash">Hash of the manifest for this shard</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if shard was completed successfully</returns>
    Task<bool> CompleteShardAsync(int shardId, bool isSuccessful, int actualItemCount, long actualDataSizeBytes, 
        string? manifestHash = null, string? errorMessage = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all shards for a parent job
    /// </summary>
    /// <param name="parentJobId">Parent job identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of job shards</returns>
    Task<List<JobShard>> GetJobShardsAsync(int parentJobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets overall progress for a sharded job
    /// </summary>
    /// <param name="parentJobId">Parent job identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job progress summary</returns>
    Task<JobShardProgress> GetJobProgressAsync(int parentJobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Handles shard retry logic when a shard fails
    /// </summary>
    /// <param name="shardId">Shard identifier</param>
    /// <param name="errorMessage">Error that caused the failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if retry was scheduled</returns>
    Task<bool> RetryShardAsync(int shardId, string errorMessage, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates if a job should be sharded based on size and complexity
    /// </summary>
    /// <param name="request">Collection request to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sharding recommendation</returns>
    Task<ShardingRecommendation> EvaluateShardingNeedAsync(CreateShardedJobRequest request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up expired shard locks and failed shards
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of locks cleaned up</returns>
    Task<int> CleanupExpiredShardLocksAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Progress summary for a sharded job
/// </summary>
public class JobShardProgress
{
    public int ParentJobId { get; set; }
    public int TotalShards { get; set; }
    public int CompletedShards { get; set; }
    public int FailedShards { get; set; }
    public int ProcessingShards { get; set; }
    public int PendingShards { get; set; }
    public double OverallProgressPercentage { get; set; }
    public long TotalItemsProcessed { get; set; }
    public long TotalBytesProcessed { get; set; }
    public long EstimatedTotalItems { get; set; }
    public long EstimatedTotalBytes { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public DateTime? EstimatedCompletionTime { get; set; }
    public Dictionary<string, int> ShardStatusCounts { get; set; } = new();
    public List<JobShard> ActiveShards { get; set; } = new();
}

/// <summary>
/// Recommendation for whether and how to shard a job
/// </summary>
public class ShardingRecommendation
{
    public bool ShouldShard { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RecommendedShardCount { get; set; }
    public TimeSpan RecommendedDateWindowSize { get; set; }
    public List<string> ShardingFactors { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}
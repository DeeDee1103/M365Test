using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

/// <summary>
/// Represents a shard of a large collection job, partitioned by custodian and date window
/// </summary>
public class JobShard
{
    public int Id { get; set; }
    
    [Required]
    public int ParentJobId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string CustodianEmail { get; set; } = string.Empty;
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    [Required]
    public CollectionJobType JobType { get; set; }
    
    [Required]
    public JobShardStatus Status { get; set; } = JobShardStatus.Pending;
    
    [Required]
    public CollectionRoute Route { get; set; }
    
    // Shard metadata
    public int ShardIndex { get; set; }
    public int TotalShards { get; set; }
    
    [StringLength(100)]
    public string ShardIdentifier { get; set; } = string.Empty; // Format: "custodian_YYYYMMDD_YYYYMMDD"
    
    // Assignment and locking
    public int? AssignedUserId { get; set; }
    
    [StringLength(50)]
    public string? AssignedWorkerId { get; set; }
    
    public DateTime? AssignedAt { get; set; }
    
    [StringLength(36)]
    public string? LockToken { get; set; }
    
    public DateTime? LockExpiry { get; set; }
    
    // Execution tracking
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }
    
    // Estimated vs actual metrics
    public long EstimatedDataSizeBytes { get; set; }
    public int EstimatedItemCount { get; set; }
    public long ActualDataSizeBytes { get; set; }
    public int ActualItemCount { get; set; }
    
    // Progress tracking
    public int ProcessedItemCount { get; set; }
    public long ProcessedDataSizeBytes { get; set; }
    public double ProgressPercentage { get; set; }
    
    [StringLength(500)]
    public string? OutputPath { get; set; }
    
    [StringLength(64)]
    public string? ManifestHash { get; set; }
    
    // Retry handling
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public DateTime? LastRetryTime { get; set; }
    
    // Navigation properties
    public virtual CollectionJob ParentJob { get; set; } = null!;
    public virtual User? AssignedUser { get; set; }
    public virtual ICollection<JobShardCheckpoint> Checkpoints { get; set; } = new List<JobShardCheckpoint>();
    public virtual ICollection<CollectedItem> CollectedItems { get; set; } = new List<CollectedItem>();
    public virtual ICollection<JobLog> JobLogs { get; set; } = new List<JobLog>();
}

/// <summary>
/// Checkpoint for tracking progress within a job shard for idempotent restarts
/// </summary>
public class JobShardCheckpoint
{
    public int Id { get; set; }
    
    [Required]
    public int JobShardId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string CheckpointType { get; set; } = string.Empty; // "MailFolderProgress", "OneDriveFileProgress", etc.
    
    [Required]
    [StringLength(500)]
    public string CheckpointKey { get; set; } = string.Empty; // Unique identifier within shard
    
    [StringLength(2000)]
    public string CheckpointData { get; set; } = string.Empty; // JSON data for restart context
    
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedDate { get; set; }
    
    [Required]
    public bool IsCompleted { get; set; } = false;
    
    // Progress within this checkpoint
    public int ItemsProcessed { get; set; }
    public long BytesProcessed { get; set; }
    
    [StringLength(36)]
    public string? CorrelationId { get; set; }
    
    // Navigation properties
    public virtual JobShard JobShard { get; set; } = null!;
}

/// <summary>
/// Configuration for job sharding strategy
/// </summary>
public class JobShardingConfig
{
    [Required]
    public string CustodianEmail { get; set; } = string.Empty;
    
    [Required]
    public CollectionJobType JobType { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    // Sharding strategy settings
    public TimeSpan MaxDateWindowSize { get; set; } = TimeSpan.FromDays(30); // Default 30-day windows
    public int MaxShardsPerCustodian { get; set; } = 12; // Max 12 shards per custodian
    public long MaxShardSizeBytes { get; set; } = 50L * 1024 * 1024 * 1024; // 50GB per shard
    public int MaxShardItemCount { get; set; } = 250000; // 250k items per shard
    
    // Advanced settings
    public bool EnableAdaptiveSharding { get; set; } = true; // Adjust shard size based on data density
    public bool PreferDateBoundaries { get; set; } = true; // Align shards to month/week boundaries when possible
    public int MinimumShardDays { get; set; } = 1; // Minimum 1-day window per shard
}

public enum JobShardStatus
{
    Pending = 1,
    Assigned = 2,
    Processing = 3,
    Running = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7,
    PartiallyCompleted = 8,
    Retrying = 9
}

/// <summary>
/// Request model for creating sharded collection jobs
/// </summary>
public class CreateShardedJobRequest
{
    [Required]
    public int MatterId { get; set; }
    
    [Required]
    public List<string> CustodianEmails { get; set; } = new();
    
    [Required]
    public CollectionJobType JobType { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    public List<string>? Keywords { get; set; }
    public bool IncludeAttachments { get; set; } = true;
    public string? OutputPath { get; set; }
    
    // Sharding configuration
    public JobShardingConfig? ShardingConfig { get; set; }
    
    // Priority settings
    public int Priority { get; set; } = 5; // 1=Highest, 10=Lowest
    public bool EnableParallelProcessing { get; set; } = true;
}

/// <summary>
/// Response model for sharded job creation
/// </summary>
public class ShardedJobResponse
{
    public int ParentJobId { get; set; }
    public List<JobShard> CreatedShards { get; set; } = new();
    public int TotalShards { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public string ShardingStrategy { get; set; } = string.Empty;
    public Dictionary<string, object> ShardingMetrics { get; set; } = new();
}
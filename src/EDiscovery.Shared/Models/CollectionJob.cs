using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class CollectionJob
{
    public int Id { get; set; }
    
    [Required]
    public int MatterId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string CustodianEmail { get; set; } = string.Empty;
    
    [Required]
    public CollectionJobType JobType { get; set; }
    
    [Required]
    public CollectionJobStatus Status { get; set; } = CollectionJobStatus.Pending;
    
    [Required]
    public CollectionRoute Route { get; set; }
    
    // User assignment and concurrency control
    public int? AssignedUserId { get; set; }
    
    [StringLength(50)]
    public string? AssignedWorkerId { get; set; }
    
    public DateTime? AssignedAt { get; set; }
    
    public int Priority { get; set; } = 5; // 1=Highest, 10=Lowest
    
    [StringLength(36)]
    public string? LockToken { get; set; }
    
    public DateTime? LockExpiry { get; set; }
    
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }
    
    public long EstimatedDataSizeBytes { get; set; }
    public int EstimatedItemCount { get; set; }
    public long ActualDataSizeBytes { get; set; }
    public int ActualItemCount { get; set; }
    
    [StringLength(500)]
    public string? OutputPath { get; set; }
    
    [StringLength(64)]
    public string? ManifestHash { get; set; }
    
    // Navigation properties
    public virtual Matter Matter { get; set; } = null!;
    public virtual User? AssignedUser { get; set; }
    public virtual ICollection<CollectedItem> CollectedItems { get; set; } = new List<CollectedItem>();
    public virtual ICollection<JobLog> JobLogs { get; set; } = new List<JobLog>();
}

public enum CollectionJobType
{
    Email = 1,
    OneDrive = 2,
    SharePoint = 3,
    Teams = 4,
    Mixed = 5
}

public enum CollectionJobStatus
{
    Pending = 1,
    Assigned = 2,
    Processing = 3,
    Running = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7,
    PartiallyCompleted = 8
}

public enum CollectionRoute
{
    GraphApi = 1,
    GraphDataConnect = 2,
    Hybrid = 3
}
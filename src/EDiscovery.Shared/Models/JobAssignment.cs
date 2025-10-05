using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class JobAssignment
{
    public int Id { get; set; }
    
    [Required]
    public int JobId { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string WorkerId { get; set; } = string.Empty; // Unique worker instance identifier
    
    [Required]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    [Required]
    public JobAssignmentStatus Status { get; set; } = JobAssignmentStatus.Assigned;
    
    public DateTime HeartbeatTime { get; set; } = DateTime.UtcNow;
    
    public int RetryCount { get; set; } = 0;
    
    [StringLength(500)]
    public string? ErrorMessage { get; set; }
    
    // Lock management
    public DateTime LockExpiry { get; set; } = DateTime.UtcNow.AddMinutes(30);
    
    [StringLength(36)]
    public string LockToken { get; set; } = Guid.NewGuid().ToString();
    
    // Navigation properties
    public virtual CollectionJob Job { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public enum JobAssignmentStatus
{
    Assigned = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Expired = 6
}
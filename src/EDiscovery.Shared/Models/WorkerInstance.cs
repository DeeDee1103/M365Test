using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class WorkerInstance
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string WorkerId { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string MachineName { get; set; } = string.Empty;
    
    [StringLength(15)]
    public string IpAddress { get; set; } = string.Empty;
    
    [Required]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    
    [Required]
    public WorkerStatus Status { get; set; } = WorkerStatus.Starting;
    
    public int MaxConcurrentJobs { get; set; } = 3;
    
    public int CurrentJobCount { get; set; } = 0;
    
    public long AvailableMemoryMB { get; set; }
    
    public double CpuUsagePercent { get; set; }
    
    [StringLength(200)]
    public string Version { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? LastError { get; set; }
    
    public DateTime? ShutdownTime { get; set; }
    
    // Navigation properties
    public virtual ICollection<JobAssignment> AssignedJobs { get; set; } = new List<JobAssignment>();
}

public enum WorkerStatus
{
    Starting = 1,
    Available = 2,
    Busy = 3,
    Overloaded = 4,
    Error = 5,
    Shutting_Down = 6,
    Offline = 7
}
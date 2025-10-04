using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class JobLog
{
    public int Id { get; set; }
    
    [Required]
    public int JobId { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [Required]
    public LogLevel Level { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Category { get; set; } = string.Empty;
    
    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;
    
    [StringLength(4000)]
    public string? Details { get; set; }
    
    [StringLength(100)]
    public string? CorrelationId { get; set; }
    
    // Navigation properties
    public virtual CollectionJob Job { get; set; } = null!;
}

public enum LogLevel
{
    Trace = 1,
    Debug = 2,
    Information = 3,
    Warning = 4,
    Error = 5,
    Critical = 6
}
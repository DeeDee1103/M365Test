using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class UserSession
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(36)]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    
    public DateTime? EndTime { get; set; }
    
    [StringLength(45)]
    public string? IpAddress { get; set; }
    
    [StringLength(500)]
    public string? UserAgent { get; set; }
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}
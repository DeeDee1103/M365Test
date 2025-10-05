using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; } = UserRole.Analyst;
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginDate { get; set; }
    
    [StringLength(50)]
    public string? Department { get; set; }
    
    [StringLength(50)]
    public string? Location { get; set; }
    
    // Concurrency control
    public int MaxConcurrentJobs { get; set; } = 5;
    
    public int MaxDataSizePerJobGB { get; set; } = 100;
    
    // Navigation properties
    public virtual ICollection<Matter> CreatedMatters { get; set; } = new List<Matter>();
    public virtual ICollection<CollectionJob> AssignedJobs { get; set; } = new List<CollectionJob>();
    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}

public enum UserRole
{
    Analyst = 1,
    SeniorAnalyst = 2,
    TeamLead = 3,
    Manager = 4,
    Administrator = 5
}
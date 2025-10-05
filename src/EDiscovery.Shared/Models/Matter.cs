using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class Matter
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [StringLength(50)]
    public string CaseNumber { get; set; } = string.Empty;
    
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;
    
    public int? CreatedByUserId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<CollectionJob> CollectionJobs { get; set; } = new List<CollectionJob>();
}
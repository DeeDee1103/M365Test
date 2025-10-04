using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

public class CollectedItem
{
    public int Id { get; set; }
    
    [Required]
    public int JobId { get; set; }
    
    [Required]
    [StringLength(500)]
    public string ItemId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string ItemType { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Subject { get; set; }
    
    [StringLength(500)]
    public string? From { get; set; }
    
    [StringLength(2000)]
    public string? To { get; set; }
    
    public DateTime? ItemDate { get; set; }
    
    [Required]
    public DateTime CollectedDate { get; set; } = DateTime.UtcNow;
    
    public long SizeBytes { get; set; }
    
    [Required]
    [StringLength(64)]
    public string Sha256Hash { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? FilePath { get; set; }
    
    [StringLength(500)]
    public string? ErrorMessage { get; set; }
    
    public bool IsSuccessful { get; set; } = true;
    
    // Navigation properties
    public virtual CollectionJob Job { get; set; } = null!;
}
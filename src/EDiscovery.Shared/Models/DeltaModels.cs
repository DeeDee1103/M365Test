using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Models;

/// <summary>
/// Represents a Delta cursor for tracking incremental changes in Microsoft Graph
/// </summary>
public class DeltaCursor
{
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for the delta tracking scope (e.g., "mail:{custodian}", "onedrive:{custodian}")
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ScopeId { get; set; } = string.Empty;

    /// <summary>
    /// Type of data being tracked (Mail, OneDrive, etc.)
    /// </summary>
    [Required]
    public DeltaType DeltaType { get; set; }

    /// <summary>
    /// Custodian email for user-specific delta tracking
    /// </summary>
    [Required]
    [StringLength(200)]
    public string CustodianEmail { get; set; } = string.Empty;

    /// <summary>
    /// Microsoft Graph delta token for incremental queries
    /// </summary>
    [Required]
    [StringLength(2000)]
    public string DeltaToken { get; set; } = string.Empty;

    /// <summary>
    /// Last successful delta query timestamp
    /// </summary>
    [Required]
    public DateTime LastDeltaTime { get; set; }

    /// <summary>
    /// Initial baseline collection completion timestamp
    /// </summary>
    public DateTime? BaselineCompletedAt { get; set; }

    /// <summary>
    /// Number of items processed in last delta query
    /// </summary>
    public int LastDeltaItemCount { get; set; }

    /// <summary>
    /// Size of data processed in last delta query (bytes)
    /// </summary>
    public long LastDeltaSizeBytes { get; set; }

    /// <summary>
    /// Total number of delta queries performed
    /// </summary>
    public int DeltaQueryCount { get; set; }

    /// <summary>
    /// Whether delta tracking is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp
    /// </summary>
    [Required]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if delta query failed
    /// </summary>
    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Job ID that created this delta cursor
    /// </summary>
    public int? CollectionJobId { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    [StringLength(4000)]
    public string? Metadata { get; set; }

    // Navigation properties
    public CollectionJob? CollectionJob { get; set; }
}

/// <summary>
/// Types of data that support delta queries
/// </summary>
public enum DeltaType
{
    Mail = 1,
    OneDrive = 2,
    SharePoint = 3,
    Teams = 4,
    Calendar = 5
}

/// <summary>
/// Result of a delta query operation
/// </summary>
public class DeltaQueryResult
{
    /// <summary>
    /// Number of items returned in this delta query
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Total size of items returned (bytes)
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// New delta token for next query
    /// </summary>
    public string? NextDeltaToken { get; set; }

    /// <summary>
    /// Whether more results are available
    /// </summary>
    public bool HasMoreResults { get; set; }

    /// <summary>
    /// Items collected in this delta query
    /// </summary>
    public List<CollectedItem> Items { get; set; } = new();

    /// <summary>
    /// Performance metrics
    /// </summary>
    public TimeSpan QueryDuration { get; set; }

    /// <summary>
    /// Error information if query failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether this was a successful delta query
    /// </summary>
    public bool IsSuccessful { get; set; } = true;
}


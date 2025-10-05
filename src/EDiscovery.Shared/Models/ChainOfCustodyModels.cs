using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EDiscovery.Shared.Models;

/// <summary>
/// Represents a tamper-evident manifest for a collection job with cryptographic integrity protection
/// </summary>
public class JobManifest
{
    public int Id { get; set; }
    
    [Required]
    public int JobId { get; set; }
    
    [Required]
    [StringLength(36)]
    public string ManifestId { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public ManifestFormat Format { get; set; } = ManifestFormat.JSON;
    
    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    public DateTime FinalizedDate { get; set; }
    
    [Required]
    [StringLength(64)]
    public string ManifestHash { get; set; } = string.Empty;
    
    [StringLength(4000)]
    public string? DigitalSignature { get; set; }
    
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [Required]
    [StringLength(500)]
    public string ImmutableStoragePath { get; set; } = string.Empty;
    
    [Required]
    public long TotalItems { get; set; }
    
    [Required]
    public long TotalSizeBytes { get; set; }
    
    [Required]
    public long SuccessfulItems { get; set; }
    
    [Required]
    public long FailedItems { get; set; }
    
    [StringLength(64)]
    public string? ImmutablePolicyId { get; set; }
    
    [Required]
    public bool IsSealed { get; set; } = false;
    
    [StringLength(36)]
    public string CreatedByCorrelationId { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string CreatedByWorker { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual CollectionJob Job { get; set; } = null!;
    public virtual ICollection<ManifestVerification> Verifications { get; set; } = new List<ManifestVerification>();
}

/// <summary>
/// Represents a verification record for manifest integrity checks
/// </summary>
public class ManifestVerification
{
    public int Id { get; set; }
    
    [Required]
    public int ManifestId { get; set; }
    
    [Required]
    public DateTime VerificationDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    public VerificationType Type { get; set; }
    
    [Required]
    public VerificationResult Result { get; set; }
    
    [StringLength(64)]
    public string? ComputedHash { get; set; }
    
    [StringLength(64)]
    public string? ExpectedHash { get; set; }
    
    [Required]
    public bool SignatureValid { get; set; }
    
    [StringLength(1000)]
    public string? ErrorDetails { get; set; }
    
    [Required]
    [StringLength(36)]
    public string VerificationId { get; set; } = Guid.NewGuid().ToString();
    
    [StringLength(36)]
    public string CorrelationId { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string VerifiedBy { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual JobManifest Manifest { get; set; } = null!;
}

/// <summary>
/// Detailed manifest entry for each collected item - used for JSON/CSV generation
/// </summary>
public class ManifestEntry
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;
    
    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;
    
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }
    
    [JsonPropertyName("from")]
    public string? From { get; set; }
    
    [JsonPropertyName("to")]
    public string? To { get; set; }
    
    [JsonPropertyName("itemDate")]
    public DateTime? ItemDate { get; set; }
    
    [JsonPropertyName("collectedDate")]
    public DateTime CollectedDate { get; set; }
    
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
    
    [JsonPropertyName("sha256Hash")]
    public string Sha256Hash { get; set; } = string.Empty;
    
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
    
    [JsonPropertyName("isSuccessful")]
    public bool IsSuccessful { get; set; }
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
    
    [JsonPropertyName("collectionSequence")]
    public int CollectionSequence { get; set; }
    
    [JsonPropertyName("custodian")]
    public string Custodian { get; set; } = string.Empty;
    
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Complete manifest structure for JSON serialization
/// </summary>
public class JobManifestData
{
    [JsonPropertyName("manifestId")]
    public string ManifestId { get; set; } = string.Empty;
    
    [JsonPropertyName("jobId")]
    public int JobId { get; set; }
    
    [JsonPropertyName("matterId")]
    public int MatterId { get; set; }
    
    [JsonPropertyName("custodian")]
    public string Custodian { get; set; } = string.Empty;
    
    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = string.Empty;
    
    [JsonPropertyName("collectionRoute")]
    public string CollectionRoute { get; set; } = string.Empty;
    
    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }
    
    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }
    
    [JsonPropertyName("finalizedDate")]
    public DateTime FinalizedDate { get; set; }
    
    [JsonPropertyName("totalItems")]
    public long TotalItems { get; set; }
    
    [JsonPropertyName("successfulItems")]
    public long SuccessfulItems { get; set; }
    
    [JsonPropertyName("failedItems")]
    public long FailedItems { get; set; }
    
    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }
    
    [JsonPropertyName("estimatedSizeBytes")]
    public long EstimatedSizeBytes { get; set; }
    
    [JsonPropertyName("createdByWorker")]
    public string CreatedByWorker { get; set; } = string.Empty;
    
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;
    
    [JsonPropertyName("items")]
    public List<ManifestEntry> Items { get; set; } = new();
    
    [JsonPropertyName("integrity")]
    public ManifestIntegrity Integrity { get; set; } = new();
}

/// <summary>
/// Integrity metadata for the manifest
/// </summary>
public class ManifestIntegrity
{
    [JsonPropertyName("hashAlgorithm")]
    public string HashAlgorithm { get; set; } = "SHA-256";
    
    [JsonPropertyName("manifestHash")]
    public string ManifestHash { get; set; } = string.Empty;
    
    [JsonPropertyName("itemsHash")]
    public string ItemsHash { get; set; } = string.Empty;
    
    [JsonPropertyName("signatureAlgorithm")]
    public string? SignatureAlgorithm { get; set; }
    
    [JsonPropertyName("digitalSignature")]
    public string? DigitalSignature { get; set; }
    
    [JsonPropertyName("signingCertificateThumbprint")]
    public string? SigningCertificateThumbprint { get; set; }
    
    [JsonPropertyName("timestampAuthority")]
    public string? TimestampAuthority { get; set; }
    
    [JsonPropertyName("immutablePolicyId")]
    public string? ImmutablePolicyId { get; set; }
    
    [JsonPropertyName("wormCompliant")]
    public bool WormCompliant { get; set; }
}

public enum ManifestFormat
{
    JSON = 1,
    CSV = 2,
    Both = 3
}

public enum VerificationType
{
    HashVerification = 1,
    SignatureVerification = 2,
    ImmutabilityVerification = 3,
    PeriodicAudit = 4,
    ChainOfCustodyValidation = 5
}

public enum VerificationResult
{
    Valid = 1,
    Invalid = 2,
    Inconclusive = 3,
    Error = 4
}
using System.Text.Json.Serialization;

namespace HybridGraphCollectorWorker.Models;

/// <summary>
/// Represents a SharePoint/OneDrive file record from GDC dataset JSON
/// </summary>
public class GdcFileRecord
{
    [JsonPropertyName("driveId")]
    public string? DriveId { get; set; }

    [JsonPropertyName("id")]
    public string? ItemId { get; set; }

    [JsonPropertyName("parentReference")]
    public GdcParentReference? ParentReference { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }

    [JsonPropertyName("siteId")]
    public string? SiteId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public DateTime? LastModifiedDateTime { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("file")]
    public GdcFileMetadata? File { get; set; }

    [JsonPropertyName("folder")]
    public GdcFolderMetadata? Folder { get; set; }

    [JsonPropertyName("createdDateTime")]
    public DateTime? CreatedDateTime { get; set; }

    [JsonPropertyName("createdBy")]
    public GdcUserInfo? CreatedBy { get; set; }

    [JsonPropertyName("lastModifiedBy")]
    public GdcUserInfo? LastModifiedBy { get; set; }

    // Helper properties
    public string? ResolvedDriveId => DriveId ?? ParentReference?.DriveId;
    public string? ResolvedFileName => FileName ?? Name;
    public bool IsFile => File != null && Folder == null;
}

/// <summary>
/// Parent reference information from GDC data
/// </summary>
public class GdcParentReference
{
    [JsonPropertyName("driveId")]
    public string? DriveId { get; set; }

    [JsonPropertyName("driveType")]
    public string? DriveType { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("siteId")]
    public string? SiteId { get; set; }
}

/// <summary>
/// File metadata from GDC data
/// </summary>
public class GdcFileMetadata
{
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("hashes")]
    public GdcFileHashes? Hashes { get; set; }
}

/// <summary>
/// File hash information from GDC data
/// </summary>
public class GdcFileHashes
{
    [JsonPropertyName("quickXorHash")]
    public string? QuickXorHash { get; set; }

    [JsonPropertyName("sha1Hash")]
    public string? Sha1Hash { get; set; }

    [JsonPropertyName("sha256Hash")]
    public string? Sha256Hash { get; set; }
}

/// <summary>
/// Folder metadata from GDC data
/// </summary>
public class GdcFolderMetadata
{
    [JsonPropertyName("childCount")]
    public int? ChildCount { get; set; }
}

/// <summary>
/// User information from GDC data
/// </summary>
public class GdcUserInfo
{
    [JsonPropertyName("user")]
    public GdcUser? User { get; set; }

    [JsonPropertyName("application")]
    public GdcApplication? Application { get; set; }
}

/// <summary>
/// User details from GDC data
/// </summary>
public class GdcUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// Application details from GDC data
/// </summary>
public class GdcApplication
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// Result of processing a GDC file record
/// </summary>
public class GdcFileProcessResult
{
    public string RecordKey { get; set; } = string.Empty;
    public GdcFileRecord Record { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SkipReason { get; set; }
    public long BytesDownloaded { get; set; }
    public string? Sha256Hash { get; set; }
    public string? OutputPath { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// Manifest entry for downloaded files
/// </summary>
public class GdcManifestEntry
{
    public string Custodian { get; set; } = string.Empty;
    public string Kind { get; set; } = "File";
    public string? DriveId { get; set; }
    public string? ItemId { get; set; }
    public string? Path { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public DateTime CollectedUtc { get; set; }
    public string? OriginalPath { get; set; }
    public string? MimeType { get; set; }
    public DateTime? LastModifiedDateTime { get; set; }
}
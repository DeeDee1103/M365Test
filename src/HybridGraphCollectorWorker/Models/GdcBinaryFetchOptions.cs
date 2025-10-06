namespace HybridGraphCollectorWorker.Models;

/// <summary>
/// Configuration options for GDC Binary Fetch functionality
/// </summary>
public class GdcBinaryFetchOptions
{
    public const string SectionName = "Gdc";

    /// <summary>
    /// Input source configuration for GDC datasets
    /// </summary>
    public GdcInputOptions Input { get; set; } = new();

    /// <summary>
    /// Filters for processing GDC data
    /// </summary>
    public GdcFiltersOptions Filters { get; set; } = new();

    /// <summary>
    /// Parallelism and performance settings
    /// </summary>
    public int Parallelism { get; set; } = 8;

    /// <summary>
    /// Maximum errors before stopping job
    /// </summary>
    public int MaxErrors { get; set; } = 100;

    /// <summary>
    /// Dry run mode - validate IDs without downloading binaries
    /// </summary>
    public bool DryRun { get; set; } = false;
}

/// <summary>
/// Input source configuration
/// </summary>
public class GdcInputOptions
{
    /// <summary>
    /// Input source type: "blob" or "nas"
    /// </summary>
    public string Kind { get; set; } = "nas";

    /// <summary>
    /// Blob storage configuration
    /// </summary>
    public GdcBlobInputOptions Blob { get; set; } = new();

    /// <summary>
    /// NAS/local file system configuration
    /// </summary>
    public GdcNasInputOptions Nas { get; set; } = new();
}

/// <summary>
/// Blob storage input configuration
/// </summary>
public class GdcBlobInputOptions
{
    /// <summary>
    /// Azure Storage connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for GDC output
    /// </summary>
    public string Container { get; set; } = "gdc-out";

    /// <summary>
    /// Prefix path for files within container
    /// </summary>
    public string Prefix { get; set; } = "files/";
}

/// <summary>
/// NAS/local file system input configuration
/// </summary>
public class GdcNasInputOptions
{
    /// <summary>
    /// Root directory for GDC output files
    /// </summary>
    public string Root { get; set; } = "./gdc-out";
}

/// <summary>
/// Filters for GDC data processing
/// </summary>
public class GdcFiltersOptions
{
    /// <summary>
    /// Custodians to process (* for all)
    /// </summary>
    public string[] Custodians { get; set; } = { "*" };

    /// <summary>
    /// File extensions to process (* for all)
    /// </summary>
    public string[] FileExtensions { get; set; } = { "*" };

    /// <summary>
    /// Only process files modified after this date (null for all)
    /// </summary>
    public DateTime? ModifiedAfter { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace HybridGraphCollectorWorker.Models
{
    /// <summary>
    /// Represents an item in a manifest for reconciliation
    /// </summary>
    public class ManifestItem
    {
        public string Custodian { get; set; } = string.Empty;
        public string? DriveId { get; set; }
        public string? ItemId { get; set; }
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? Sha256 { get; set; }
        public string? StorageUri { get; set; }
        public DateTime? CollectedUtc { get; set; }
        public string? Kind { get; set; }

        /// <summary>
        /// Primary key for reconciliation (ItemId preferred)
        /// </summary>
        public string GetPrimaryKey()
        {
            if (!string.IsNullOrEmpty(ItemId))
                return ItemId;
            
            // Fallback key: DriveId + Path + Size + LastModified
            return $"{DriveId}|{Path}|{Size}|{LastModified:yyyy-MM-ddTHH:mm:ssZ}";
        }

        /// <summary>
        /// Normalize path for comparison
        /// </summary>
        public string GetNormalizedPath()
        {
            return Path?.Replace('\\', '/').Trim('/').ToLowerInvariant() ?? string.Empty;
        }

        /// <summary>
        /// Check if this is a folder item
        /// </summary>
        public bool IsFolder()
        {
            return Kind?.Equals("folder", StringComparison.OrdinalIgnoreCase) == true ||
                   Path?.EndsWith("/") == true;
        }
    }

    /// <summary>
    /// Result of reconciliation process
    /// </summary>
    public class ReconciliationResult
    {
        public string JobId { get; set; } = string.Empty;
        public string Custodian { get; set; } = string.Empty;
        public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;

        // Counts
        public long SourceCount { get; set; }
        public long CollectedCount { get; set; }
        public long MissedCount { get; set; }
        public long ExtraCount { get; set; }
        public long HashMismatchCount { get; set; }
        public long ExpectedSkipsCount { get; set; }

        // Sizes
        public long SourceTotalBytes { get; set; }
        public long CollectedTotalBytes { get; set; }
        public long SizeDeltaBytes => CollectedTotalBytes - SourceTotalBytes;

        // Tolerances and gates
        public double SizeTolerancePct { get; set; }
        public double ExtraTolerancePct { get; set; }
        public bool RequireHashMatch { get; set; }

        // Results
        public bool CardinalityPassed => MissedCount == 0;
        public bool ExtrasPassed => ExtraTolerancePct == 0 ? ExtraCount == 0 : 
            (SourceCount == 0 ? ExtraCount == 0 : (double)ExtraCount / SourceCount * 100 <= ExtraTolerancePct);
        public bool SizePassed => SourceTotalBytes == 0 ? CollectedTotalBytes == 0 :
            Math.Abs(SizeDeltaBytes) / (double)SourceTotalBytes * 100 <= SizeTolerancePct;
        public bool HashPassed => !RequireHashMatch || HashMismatchCount == 0;

        public bool OverallPassed => CardinalityPassed && ExtrasPassed && SizePassed && HashPassed;

        // Report details
        public List<DiscrepancyItem> Missed { get; set; } = new();
        public List<DiscrepancyItem> Extras { get; set; } = new();
        public List<DiscrepancyItem> HashMismatches { get; set; } = new();
        public List<DiscrepancyItem> ExpectedSkips { get; set; } = new();

        public string ReportPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a discrepancy found during reconciliation
    /// </summary>
    public class DiscrepancyItem
    {
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Missed", "Extra", "HashMismatch", "ExpectedSkip"
        public string Custodian { get; set; } = string.Empty;
        public string? DriveId { get; set; }
        public string? ItemId { get; set; }
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? SourceSha256 { get; set; }
        public string? CollectedSha256 { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime DetectedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Reconciliation statistics for API reporting
    /// </summary>
    public class ReconciliationStats
    {
        public long? ReconMissedCount { get; set; }
        public long? ReconExtraCount { get; set; }
        public long? ReconHashMismatchCount { get; set; }
        public long? ReconSourceBytes { get; set; }
        public long? ReconCollectedBytes { get; set; }
        public bool? ReconPassed { get; set; }
        public string? ReconReportPath { get; set; }
        public DateTime? ReconProcessedUtc { get; set; }
    }
}
namespace HybridGraphCollectorWorker.Models
{
    /// <summary>
    /// Configuration options for reconciliation between source and collected manifests
    /// </summary>
    public class ReconcileOptions
    {
        /// <summary>
        /// Path to source manifests (CSV/JSON/Parquet from Graph/GDC)
        /// </summary>
        public string SourceManifestPath { get; set; } = "./manifests/source";

        /// <summary>
        /// Path to collected manifests (CSV/JSON from collection process)
        /// </summary>
        public string CollectedManifestPath { get; set; } = "./manifests/collected";

        /// <summary>
        /// Path to output reconciliation reports
        /// </summary>
        public string ReportsPath { get; set; } = "./manifests/reports";

        /// <summary>
        /// Size tolerance percentage (e.g., 0.1 = 0.1% tolerance)
        /// </summary>
        public double SizeTolerancePct { get; set; } = 0.1;

        /// <summary>
        /// Extra items tolerance percentage (e.g., 0.05 = 0.05% extras allowed)
        /// </summary>
        public double ExtraTolerancePct { get; set; } = 0.05;

        /// <summary>
        /// Whether to require SHA-256 hash matches when available
        /// </summary>
        public bool RequireHashMatch { get; set; } = false;

        /// <summary>
        /// Soft fail mode - mark as CompletedWithWarnings instead of Failed
        /// </summary>
        public bool SoftFail { get; set; } = false;

        /// <summary>
        /// Enable dry-run mode - print summary without writing files
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Normalize paths for comparison (case, slashes)
        /// </summary>
        public bool NormalizePaths { get; set; } = true;

        /// <summary>
        /// Treat folders as collectable items
        /// </summary>
        public bool IncludeFolders { get; set; } = false;
    }
}
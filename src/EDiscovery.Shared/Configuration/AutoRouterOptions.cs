using System.ComponentModel.DataAnnotations;

namespace EDiscovery.Shared.Configuration;

/// <summary>
/// Configuration options for the AutoRouter service routing decisions
/// </summary>
public class AutoRouterOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "AutoRouter";

    /// <summary>
    /// Graph API threshold settings
    /// </summary>
    public GraphApiThresholds GraphApiThresholds { get; set; } = new();

    /// <summary>
    /// Routing confidence level settings
    /// </summary>
    public RoutingConfidence RoutingConfidence { get; set; } = new();
}

/// <summary>
/// Threshold settings for Graph API routing decisions
/// </summary>
public class GraphApiThresholds
{
    /// <summary>
    /// Maximum data size in bytes for Graph API routing (default: 100GB)
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "MaxSizeBytes must be greater than 0")]
    public long MaxSizeBytes { get; set; } = 107374182400L; // 100GB

    /// <summary>
    /// Maximum item count for Graph API routing (default: 500k items)
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "MaxItemCount must be greater than 0")]
    public int MaxItemCount { get; set; } = 500_000;

    /// <summary>
    /// Description of the thresholds for documentation
    /// </summary>
    public string Description { get; set; } = "Graph API routing thresholds";

    /// <summary>
    /// Convert MaxSizeBytes to human-readable format
    /// </summary>
    public string MaxSizeFormatted => FormatBytes(MaxSizeBytes);

    /// <summary>
    /// Convert MaxItemCount to human-readable format
    /// </summary>
    public string MaxItemCountFormatted => MaxItemCount.ToString("N0");

    /// <summary>
    /// Helper method to format bytes to human-readable string
    /// </summary>
    /// <param name="bytes">Bytes to format</param>
    /// <returns>Formatted string (e.g., "100 GB")</returns>
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

/// <summary>
/// Confidence level settings for routing decisions
/// </summary>
public class RoutingConfidence
{
    /// <summary>
    /// High confidence percentage (default: 90%)
    /// </summary>
    [Range(0, 100, ErrorMessage = "HighConfidence must be between 0 and 100")]
    public double HighConfidence { get; set; } = 90.0;

    /// <summary>
    /// Medium confidence percentage (default: 80%)
    /// </summary>
    [Range(0, 100, ErrorMessage = "MediumConfidence must be between 0 and 100")]
    public double MediumConfidence { get; set; } = 80.0;

    /// <summary>
    /// Low confidence percentage (default: 70%)
    /// </summary>
    [Range(0, 100, ErrorMessage = "LowConfidence must be between 0 and 100")]
    public double LowConfidence { get; set; } = 70.0;

    /// <summary>
    /// Convert confidence levels to decimal format (0.0 - 1.0)
    /// </summary>
    public double HighConfidenceDecimal => HighConfidence / 100.0;
    public double MediumConfidenceDecimal => MediumConfidence / 100.0;
    public double LowConfidenceDecimal => LowConfidence / 100.0;
}

/// <summary>
/// Configuration options for Delta Query functionality
/// </summary>
public class DeltaQueryOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "DeltaQuery";

    /// <summary>
    /// Enable delta queries for Mail collections
    /// </summary>
    public bool EnableMailDelta { get; set; } = true;

    /// <summary>
    /// Enable delta queries for OneDrive collections
    /// </summary>
    public bool EnableOneDriveDelta { get; set; } = true;

    /// <summary>
    /// Maximum age of delta cursor before forcing full resync (days)
    /// </summary>
    [Range(1, 365, ErrorMessage = "MaxDeltaAgeDays must be between 1 and 365")]
    public int MaxDeltaAgeDays { get; set; } = 30;

    /// <summary>
    /// How often to perform delta queries (minutes)
    /// </summary>
    [Range(1, 1440, ErrorMessage = "DeltaQueryIntervalMinutes must be between 1 and 1440")]
    public int DeltaQueryIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of items to process in a single delta query
    /// </summary>
    [Range(1, 10000, ErrorMessage = "MaxDeltaItemsPerQuery must be between 1 and 10000")]
    public int MaxDeltaItemsPerQuery { get; set; } = 1000;

    /// <summary>
    /// Force full resync if delta query fails this many times
    /// </summary>
    [Range(1, 10, ErrorMessage = "MaxDeltaFailures must be between 1 and 10")]
    public int MaxDeltaFailures { get; set; } = 3;

    /// <summary>
    /// Whether to perform delta queries in background
    /// </summary>
    public bool BackgroundDeltaQueries { get; set; } = true;

    /// <summary>
    /// Enable automatic cleanup of stale delta cursors
    /// </summary>
    public bool EnableAutomaticCleanup { get; set; } = true;

    /// <summary>
    /// How often to run cleanup process (hours)
    /// </summary>
    [Range(1, 168, ErrorMessage = "CleanupIntervalHours must be between 1 and 168")]
    public int CleanupIntervalHours { get; set; } = 24;
}
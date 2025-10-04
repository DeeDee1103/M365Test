namespace EDiscovery.Shared.Models;

public class AutoRouterDecision
{
    public CollectionRoute RecommendedRoute { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long EstimatedDataSizeBytes { get; set; }
    public int EstimatedItemCount { get; set; }
    public double ConfidenceScore { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public class CollectionQuota
{
    public long UsedBytes { get; set; }
    public long LimitBytes { get; set; }
    public int UsedItems { get; set; }
    public int LimitItems { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CollectionRequest
{
    public string CustodianEmail { get; set; } = string.Empty;
    public CollectionJobType JobType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string> Keywords { get; set; } = new();
    public bool IncludeAttachments { get; set; } = true;
    public string OutputPath { get; set; } = string.Empty;
}
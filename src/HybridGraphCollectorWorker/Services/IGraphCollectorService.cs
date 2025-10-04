using EDiscovery.Shared.Models;

namespace HybridGraphCollectorWorker.Services;

public interface IGraphCollectorService
{
    Task<CollectionResult> CollectEmailAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResult> CollectOneDriveAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResult> CollectSharePointAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResult> CollectTeamsAsync(CollectionRequest request, CancellationToken cancellationToken = default);
}

public class CollectionResult
{
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CollectedItem> Items { get; set; } = new();
    public long TotalSizeBytes { get; set; }
    public int TotalItemCount { get; set; }
    public string? ManifestHash { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}
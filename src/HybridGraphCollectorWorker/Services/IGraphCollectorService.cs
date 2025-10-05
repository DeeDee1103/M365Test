using EDiscovery.Shared.Models;

namespace HybridGraphCollectorWorker.Services;

public interface IGraphCollectorService
{
    Task<CollectionResult> CollectEmailAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResult> CollectOneDriveAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResult> CollectSharePointAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResult> CollectTeamsAsync(CollectionRequest request, CancellationToken cancellationToken = default);
}
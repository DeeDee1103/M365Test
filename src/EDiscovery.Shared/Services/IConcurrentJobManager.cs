using EDiscovery.Shared.Models;

namespace EDiscovery.Shared.Services;

public interface IConcurrentJobManager
{
    Task<CollectionJob?> GetNextAvailableJobAsync(string workerId, int userId, CancellationToken cancellationToken = default);
    Task<bool> AcquireJobLockAsync(int jobId, string workerId, int userId, CancellationToken cancellationToken = default);
    Task ReleaseJobLockAsync(int jobId, string workerId, CancellationToken cancellationToken = default);
    Task UpdateJobHeartbeatAsync(int jobId, string workerId, CancellationToken cancellationToken = default);
    Task<bool> CanUserProcessMoreJobsAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> IsWorkerOverloadedAsync(string workerId, CancellationToken cancellationToken = default);
    Task RegisterWorkerAsync(WorkerInstance worker, CancellationToken cancellationToken = default);
    Task UpdateWorkerStatusAsync(string workerId, WorkerStatus status, CancellationToken cancellationToken = default);
    Task<List<CollectionJob>> GetActiveJobsForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<WorkerInstance>> GetActiveWorkersAsync(CancellationToken cancellationToken = default);
    Task CleanupExpiredLocksAsync(CancellationToken cancellationToken = default);
}
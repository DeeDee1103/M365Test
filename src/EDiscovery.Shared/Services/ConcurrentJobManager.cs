using EDiscovery.Shared.Models;
using EDiscovery.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EDiscovery.Shared.Services;

public class ConcurrentJobManager : IConcurrentJobManager
{
    private readonly ILogger<ConcurrentJobManager> _logger;
    private readonly IComplianceLogger _complianceLogger;
    private readonly IDbContextFactory<EDiscoveryDbContext> _contextFactory;

    public ConcurrentJobManager(
        ILogger<ConcurrentJobManager> logger,
        IComplianceLogger complianceLogger,
        IDbContextFactory<EDiscoveryDbContext> contextFactory)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;
        _contextFactory = contextFactory;
    }

    public async Task<CollectionJob?> GetNextAvailableJobAsync(string workerId, int userId, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Check if user can process more jobs
            if (!await CanUserProcessMoreJobsAsync(userId, cancellationToken))
            {
                _logger.LogWarning("User {UserId} has reached maximum concurrent job limit | CorrelationId: {CorrelationId}", 
                    userId, correlationId);
                return null;
            }

            // Check if worker is overloaded
            if (await IsWorkerOverloadedAsync(workerId, cancellationToken))
            {
                _logger.LogWarning("Worker {WorkerId} is overloaded | CorrelationId: {CorrelationId}", 
                    workerId, correlationId);
                return null;
            }

            // Get next available job with pessimistic locking
            var job = await context.CollectionJobs
                .Where(j => j.Status == CollectionJobStatus.Pending && 
                           (j.LockExpiry == null || j.LockExpiry < DateTime.UtcNow))
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (job == null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            // Acquire lock
            if (await AcquireJobLockAsync(job.Id, workerId, userId, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                
                _complianceLogger.LogAudit("JobAcquired", new 
                {
                    JobId = job.Id,
                    WorkerId = workerId,
                    UserId = userId,
                    Priority = job.Priority
                }, job.CustodianEmail, correlationId);
                
                return job;
            }

            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error getting next available job for worker {WorkerId} | CorrelationId: {CorrelationId}", 
                workerId, correlationId);
            throw;
        }
    }

    public async Task<bool> AcquireJobLockAsync(int jobId, string workerId, int userId, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            var lockToken = Guid.NewGuid().ToString();
            var lockExpiry = DateTime.UtcNow.AddMinutes(30);
            
            var rowsAffected = await context.Database.ExecuteSqlRawAsync(
                @"UPDATE CollectionJobs 
                  SET AssignedUserId = {0}, AssignedWorkerId = {1}, AssignedAt = {2}, 
                      LockToken = {3}, LockExpiry = {4}, Status = {5}
                  WHERE Id = {6} AND (LockExpiry IS NULL OR LockExpiry < {7})",
                userId, workerId, DateTime.UtcNow, lockToken, lockExpiry, 
                (int)CollectionJobStatus.Assigned, jobId, DateTime.UtcNow,
                cancellationToken);

            if (rowsAffected > 0)
            {
                // Create job assignment record
                var assignment = new JobAssignment
                {
                    JobId = jobId,
                    UserId = userId,
                    WorkerId = workerId,
                    LockToken = lockToken,
                    LockExpiry = lockExpiry
                };
                
                context.JobAssignments.Add(assignment);
                await context.SaveChangesAsync(cancellationToken);
                
                _complianceLogger.LogAudit("JobLockAcquired", new 
                {
                    JobId = jobId,
                    WorkerId = workerId,
                    UserId = userId,
                    LockToken = lockToken,
                    LockExpiry = lockExpiry
                }, null, correlationId);
                
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring job lock for job {JobId} worker {WorkerId} | CorrelationId: {CorrelationId}", 
                jobId, workerId, correlationId);
            return false;
        }
    }

    public async Task ReleaseJobLockAsync(int jobId, string workerId, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                @"UPDATE CollectionJobs 
                  SET AssignedUserId = NULL, AssignedWorkerId = NULL, AssignedAt = NULL,
                      LockToken = NULL, LockExpiry = NULL
                  WHERE Id = {0} AND AssignedWorkerId = {1}",
                jobId, workerId, cancellationToken);

            // Update assignment status
            var assignment = await context.JobAssignments
                .Where(a => a.JobId == jobId && a.WorkerId == workerId && a.Status == JobAssignmentStatus.Processing)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (assignment != null)
            {
                assignment.Status = JobAssignmentStatus.Completed;
                assignment.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }
            
            _complianceLogger.LogAudit("JobLockReleased", new 
            {
                JobId = jobId,
                WorkerId = workerId
            }, null, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing job lock for job {JobId} worker {WorkerId} | CorrelationId: {CorrelationId}", 
                jobId, workerId, correlationId);
            throw;
        }
    }

    public async Task UpdateJobHeartbeatAsync(int jobId, string workerId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            var assignment = await context.JobAssignments
                .Where(a => a.JobId == jobId && a.WorkerId == workerId && a.Status == JobAssignmentStatus.Processing)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (assignment != null)
            {
                assignment.HeartbeatTime = DateTime.UtcNow;
                assignment.LockExpiry = DateTime.UtcNow.AddMinutes(30); // Extend lock
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating heartbeat for job {JobId} worker {WorkerId}", jobId, workerId);
        }
    }

    public async Task<bool> CanUserProcessMoreJobsAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            var user = await context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null || !user.IsActive)
                return false;

            var activeJobCount = await context.CollectionJobs
                .CountAsync(j => j.AssignedUserId == userId && 
                               (j.Status == CollectionJobStatus.Assigned || j.Status == CollectionJobStatus.Processing),
                           cancellationToken);

            return activeJobCount < user.MaxConcurrentJobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user {UserId} job capacity", userId);
            return false;
        }
    }

    public async Task<bool> IsWorkerOverloadedAsync(string workerId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            var worker = await context.WorkerInstances
                .Where(w => w.WorkerId == workerId)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (worker == null)
                return true; // Unknown worker is considered overloaded

            return worker.CurrentJobCount >= worker.MaxConcurrentJobs || 
                   worker.Status == WorkerStatus.Overloaded ||
                   worker.CpuUsagePercent > 90.0 ||
                   worker.AvailableMemoryMB < 1024; // Less than 1GB available
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking worker {WorkerId} load status", workerId);
            return true; // Assume overloaded on error
        }
    }

    public async Task RegisterWorkerAsync(WorkerInstance worker, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            var existingWorker = await context.WorkerInstances
                .Where(w => w.WorkerId == worker.WorkerId)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (existingWorker != null)
            {
                existingWorker.LastHeartbeat = DateTime.UtcNow;
                existingWorker.Status = WorkerStatus.Available;
                existingWorker.MachineName = worker.MachineName;
                existingWorker.IpAddress = worker.IpAddress;
                existingWorker.Version = worker.Version;
            }
            else
            {
                context.WorkerInstances.Add(worker);
            }
            
            await context.SaveChangesAsync(cancellationToken);
            
            _complianceLogger.LogAudit("WorkerRegistered", new 
            {
                WorkerId = worker.WorkerId,
                MachineName = worker.MachineName,
                IpAddress = worker.IpAddress,
                MaxConcurrentJobs = worker.MaxConcurrentJobs
            }, null, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering worker {WorkerId} | CorrelationId: {CorrelationId}", 
                worker.WorkerId, correlationId);
            throw;
        }
    }

    public async Task UpdateWorkerStatusAsync(string workerId, WorkerStatus status, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            var worker = await context.WorkerInstances
                .Where(w => w.WorkerId == workerId)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (worker != null)
            {
                worker.Status = status;
                worker.LastHeartbeat = DateTime.UtcNow;
                
                if (status == WorkerStatus.Shutting_Down || status == WorkerStatus.Offline)
                {
                    worker.ShutdownTime = DateTime.UtcNow;
                }
                
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating worker {WorkerId} status to {Status}", workerId, status);
        }
    }

    public async Task<List<CollectionJob>> GetActiveJobsForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        return await context.CollectionJobs
            .Where(j => j.AssignedUserId == userId && 
                       (j.Status == CollectionJobStatus.Assigned || j.Status == CollectionJobStatus.Processing))
            .Include(j => j.Matter)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WorkerInstance>> GetActiveWorkersAsync(CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        return await context.WorkerInstances
            .Where(w => w.Status != WorkerStatus.Offline && 
                       w.LastHeartbeat > DateTime.UtcNow.AddMinutes(-5))
            .ToListAsync(cancellationToken);
    }

    public async Task CleanupExpiredLocksAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        try
        {
            // Release expired job locks
            var expiredJobs = await context.CollectionJobs
                .Where(j => j.LockExpiry != null && j.LockExpiry < DateTime.UtcNow)
                .ToListAsync(cancellationToken);
                
            foreach (var job in expiredJobs)
            {
                job.AssignedUserId = null;
                job.AssignedWorkerId = null;
                job.AssignedAt = null;
                job.LockToken = null;
                job.LockExpiry = null;
                job.Status = CollectionJobStatus.Pending;
            }

            // Mark expired assignments
            var expiredAssignments = await context.JobAssignments
                .Where(a => a.LockExpiry < DateTime.UtcNow && a.Status == JobAssignmentStatus.Processing)
                .ToListAsync(cancellationToken);
                
            foreach (var assignment in expiredAssignments)
            {
                assignment.Status = JobAssignmentStatus.Expired;
                assignment.CompletedAt = DateTime.UtcNow;
            }
            
            await context.SaveChangesAsync(cancellationToken);
            
            if (expiredJobs.Any() || expiredAssignments.Any())
            {
                _complianceLogger.LogAudit("ExpiredLocksCleanup", new 
                {
                    ExpiredJobsCount = expiredJobs.Count,
                    ExpiredAssignmentsCount = expiredAssignments.Count
                }, null, correlationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired locks | CorrelationId: {CorrelationId}", correlationId);
            throw;
        }
    }
}
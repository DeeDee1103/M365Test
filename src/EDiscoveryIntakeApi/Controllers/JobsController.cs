using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDiscoveryIntakeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly EDiscoveryDbContext _context;
    private readonly IAutoRouterService _autoRouter;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        EDiscoveryDbContext context, 
        IAutoRouterService autoRouter,
        ILogger<JobsController> logger)
    {
        _context = context;
        _autoRouter = autoRouter;
        _logger = logger;
    }

    /// <summary>
    /// Get all collection jobs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CollectionJob>>> GetJobs()
    {
        return await _context.CollectionJobs
            .Include(j => j.Matter)
            .Include(j => j.CollectedItems)
            .OrderByDescending(j => j.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific collection job by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CollectionJob>> GetJob(int id)
    {
        var job = await _context.CollectionJobs
            .Include(j => j.Matter)
            .Include(j => j.CollectedItems)
            .Include(j => j.JobLogs.OrderByDescending(l => l.Timestamp))
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound();
        }

        return job;
    }

    /// <summary>
    /// Create a new collection job
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CollectionJob>> CreateJob(CreateJobRequest request)
    {
        // Validate matter exists
        var matter = await _context.Matters.FindAsync(request.MatterId);
        if (matter == null)
        {
            return BadRequest("Matter not found");
        }

        // Use AutoRouter to determine optimal route
        var collectionRequest = new CollectionRequest
        {
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Keywords = request.Keywords ?? new List<string>(),
            IncludeAttachments = request.IncludeAttachments,
            OutputPath = request.OutputPath ?? string.Empty
        };

        var routerDecision = await _autoRouter.DetermineOptimalRouteAsync(collectionRequest);

        var job = new CollectionJob
        {
            MatterId = request.MatterId,
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType,
            Route = routerDecision.RecommendedRoute,
            EstimatedDataSizeBytes = routerDecision.EstimatedDataSizeBytes,
            EstimatedItemCount = routerDecision.EstimatedItemCount,
            OutputPath = request.OutputPath,
            CreatedDate = DateTime.UtcNow
        };

        _context.CollectionJobs.Add(job);
        await _context.SaveChangesAsync();

        // Log the router decision
        var log = new JobLog
        {
            JobId = job.Id,
            Level = EDiscovery.Shared.Models.LogLevel.Information,
            Category = "AutoRouter",
            Message = $"Route determined: {routerDecision.RecommendedRoute}",
            Details = $"Reason: {routerDecision.Reason}. Confidence: {routerDecision.ConfidenceScore:P1}",
            Timestamp = DateTime.UtcNow
        };
        _context.JobLogs.Add(log);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new collection job: {JobId} for custodian: {CustodianEmail} using route: {Route}", 
            job.Id, job.CustodianEmail, job.Route);

        return CreatedAtAction("GetJob", new { id = job.Id }, job);
    }

    /// <summary>
    /// Start a collection job
    /// </summary>
    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartJob(int id)
    {
        var job = await _context.CollectionJobs.FindAsync(id);
        if (job == null)
        {
            return NotFound();
        }

        if (job.Status != CollectionJobStatus.Pending)
        {
            return BadRequest("Job is not in pending status");
        }

        job.Status = CollectionJobStatus.Running;
        job.StartTime = DateTime.UtcNow;

        var log = new JobLog
        {
            JobId = job.Id,
            Level = EDiscovery.Shared.Models.LogLevel.Information,
            Category = "JobLifecycle",
            Message = "Collection job started",
            Timestamp = DateTime.UtcNow
        };
        _context.JobLogs.Add(log);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Started collection job: {JobId}", id);

        return Ok();
    }

    /// <summary>
    /// Complete a collection job
    /// </summary>
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteJob(int id, CompleteJobRequest request)
    {
        var job = await _context.CollectionJobs.FindAsync(id);
        if (job == null)
        {
            return NotFound();
        }

        if (job.Status != CollectionJobStatus.Running)
        {
            return BadRequest("Job is not in running status");
        }

        job.Status = request.IsSuccessful ? CollectionJobStatus.Completed : CollectionJobStatus.Failed;
        job.EndTime = DateTime.UtcNow;
        job.ActualDataSizeBytes = request.ActualDataSizeBytes;
        job.ActualItemCount = request.ActualItemCount;
        job.ManifestHash = request.ManifestHash;
        job.ErrorMessage = request.ErrorMessage;

        var log = new JobLog
        {
            JobId = job.Id,
            Level = request.IsSuccessful ? EDiscovery.Shared.Models.LogLevel.Information : EDiscovery.Shared.Models.LogLevel.Error,
            Category = "JobLifecycle",
            Message = request.IsSuccessful ? "Collection job completed successfully" : "Collection job failed",
            Details = request.ErrorMessage,
            Timestamp = DateTime.UtcNow
        };
        _context.JobLogs.Add(log);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Completed collection job: {JobId} with status: {Status}", id, job.Status);

        return Ok();
    }

    /// <summary>
    /// Record collected items for a job
    /// </summary>
    [HttpPost("{id}/items")]
    public async Task<IActionResult> RecordCollectedItems(int id, List<CollectedItem> items)
    {
        var job = await _context.CollectionJobs.FindAsync(id);
        if (job == null)
        {
            return NotFound();
        }

        foreach (var item in items)
        {
            item.JobId = id;
            item.CollectedDate = DateTime.UtcNow;
        }

        _context.CollectedItems.AddRange(items);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Recorded {ItemCount} collected items for job: {JobId}", items.Count, id);

        return Ok();
    }

    /// <summary>
    /// Run reconciliation validation for a completed collection job
    /// </summary>
    [HttpPost("{id}/reconcile")]
    public async Task<ActionResult> ReconcileJob(int id, [FromBody] ReconcileRequest request)
    {
        var job = await _context.CollectionJobs.FindAsync(id);
        if (job == null)
        {
            return NotFound($"Job {id} not found");
        }

        if (job.Status != CollectionJobStatus.Completed)
        {
            return BadRequest($"Job {id} is not completed and cannot be reconciled");
        }

        _logger.LogInformation("Starting reconciliation for job {JobId}", id);

        // Trigger reconciliation worker
        // Note: In production, this would enqueue a background job
        // For now, we'll return accepted and log the request
        await _context.SaveChangesAsync();

        return Accepted(new { Message = "Reconciliation request accepted", JobId = id, Request = request });
    }
}

public class CreateJobRequest
{
    public int MatterId { get; set; }
    public string CustodianEmail { get; set; } = string.Empty;
    public CollectionJobType JobType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string>? Keywords { get; set; }
    public bool IncludeAttachments { get; set; } = true;
    public string? OutputPath { get; set; }
}

public class CompleteJobRequest
{
    public bool IsSuccessful { get; set; }
    public long ActualDataSizeBytes { get; set; }
    public int ActualItemCount { get; set; }
    public string? ManifestHash { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ReconcileRequest
{
    public string? CustodianFilter { get; set; }
    public string? SourceManifestPath { get; set; }
    public string? CollectedManifestPath { get; set; }
    public bool DryRun { get; set; } = false;
}
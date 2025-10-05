using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscoveryIntakeApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDiscoveryIntakeApi.Controllers;

/// <summary>
/// Controller for managing sharded collection jobs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ShardedJobsController : ControllerBase
{
    private readonly IJobShardingService _shardingService;
    private readonly EDiscoveryDbContext _context;
    private readonly ILogger<ShardedJobsController> _logger;

    public ShardedJobsController(
        IJobShardingService shardingService,
        EDiscoveryDbContext context,
        ILogger<ShardedJobsController> logger)
    {
        _shardingService = shardingService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a new sharded collection job
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ShardedJobResponse>> CreateShardedJob(CreateShardedJobRequest request)
    {
        try
        {
            _logger.LogInformation("Creating sharded job for {CustodianCount} custodians", request.CustodianEmails.Count);

            var response = await _shardingService.CreateShardedJobAsync(request);

            _logger.LogInformation("Created sharded job {JobId} with {ShardCount} shards", 
                response.ParentJobId, response.TotalShards);

            return CreatedAtAction("GetShardedJob", new { id = response.ParentJobId }, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid request for sharded job creation: {Error}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sharded job");
            return StatusCode(500, "An error occurred while creating the sharded job");
        }
    }

    /// <summary>
    /// Get sharded job details and progress
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<JobShardProgress>> GetShardedJob(int id)
    {
        try
        {
            var progress = await _shardingService.GetJobProgressAsync(id);
            
            if (progress.TotalShards == 0)
            {
                return NotFound();
            }

            return progress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sharded job {JobId}", id);
            return StatusCode(500, "An error occurred while retrieving the sharded job");
        }
    }

    /// <summary>
    /// Get all shards for a parent job
    /// </summary>
    [HttpGet("{id}/shards")]
    public async Task<ActionResult<List<JobShard>>> GetJobShards(int id)
    {
        try
        {
            var shards = await _shardingService.GetJobShardsAsync(id);
            return shards;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shards for job {JobId}", id);
            return StatusCode(500, "An error occurred while retrieving job shards");
        }
    }

    /// <summary>
    /// Get progress details for a specific shard
    /// </summary>
    [HttpGet("shards/{shardId}")]
    public async Task<ActionResult<JobShard>> GetShard(int shardId)
    {
        try
        {
            var shard = await _context.JobShards
                .Include(s => s.ParentJob)
                .Include(s => s.Checkpoints)
                .Include(s => s.CollectedItems)
                .FirstOrDefaultAsync(s => s.Id == shardId);

            if (shard == null)
            {
                return NotFound();
            }

            return shard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shard {ShardId}", shardId);
            return StatusCode(500, "An error occurred while retrieving the shard");
        }
    }

    /// <summary>
    /// Get checkpoints for a specific shard
    /// </summary>
    [HttpGet("shards/{shardId}/checkpoints")]
    public async Task<ActionResult<List<JobShardCheckpoint>>> GetShardCheckpoints(int shardId)
    {
        try
        {
            var checkpoints = await _shardingService.GetShardCheckpointsAsync(shardId);
            return checkpoints;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkpoints for shard {ShardId}", shardId);
            return StatusCode(500, "An error occurred while retrieving shard checkpoints");
        }
    }

    /// <summary>
    /// Evaluate if a job should be sharded
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<ActionResult<ShardingRecommendation>> EvaluateSharding(CreateShardedJobRequest request)
    {
        try
        {
            var recommendation = await _shardingService.EvaluateShardingNeedAsync(request);
            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating sharding need");
            return StatusCode(500, "An error occurred while evaluating sharding need");
        }
    }

    /// <summary>
    /// Retry a failed shard
    /// </summary>
    [HttpPost("shards/{shardId}/retry")]
    public async Task<ActionResult> RetryShard(int shardId, [FromBody] RetryShardRequest request)
    {
        try
        {
            var success = await _shardingService.RetryShardAsync(shardId, request.ErrorMessage);
            
            if (!success)
            {
                return BadRequest("Shard cannot be retried (not found or max retries exceeded)");
            }

            _logger.LogInformation("Scheduled retry for shard {ShardId}", shardId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying shard {ShardId}", shardId);
            return StatusCode(500, "An error occurred while retrying the shard");
        }
    }

    /// <summary>
    /// Update progress for a shard (for worker services)
    /// </summary>
    [HttpPut("shards/{shardId}/progress")]
    public async Task<ActionResult> UpdateShardProgress(int shardId, [FromBody] UpdateShardProgressRequest request)
    {
        try
        {
            var success = await _shardingService.UpdateShardProgressAsync(
                shardId, request.ItemsProcessed, request.BytesProcessed);
            
            if (!success)
            {
                return NotFound();
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for shard {ShardId}", shardId);
            return StatusCode(500, "An error occurred while updating shard progress");
        }
    }

    /// <summary>
    /// Complete a shard (for worker services)
    /// </summary>
    [HttpPut("shards/{shardId}/complete")]
    public async Task<ActionResult> CompleteShard(int shardId, [FromBody] CompleteShardRequest request)
    {
        try
        {
            var success = await _shardingService.CompleteShardAsync(
                shardId, 
                request.IsSuccessful, 
                request.ActualItemCount, 
                request.ActualDataSizeBytes,
                request.ManifestHash,
                request.ErrorMessage);
            
            if (!success)
            {
                return NotFound();
            }

            _logger.LogInformation("Completed shard {ShardId} | Success: {Success} | Items: {Items}", 
                shardId, request.IsSuccessful, request.ActualItemCount);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing shard {ShardId}", shardId);
            return StatusCode(500, "An error occurred while completing the shard");
        }
    }

    /// <summary>
    /// Get next available shard for worker assignment
    /// </summary>
    [HttpPost("shards/next")]
    public async Task<ActionResult<JobShard?>> GetNextAvailableShard([FromBody] GetNextShardRequest request)
    {
        try
        {
            var shard = await _shardingService.GetNextAvailableShardAsync(request.WorkerId, request.UserId);
            
            if (shard != null)
            {
                // Attempt to acquire lock
                var lockAcquired = await _shardingService.AcquireShardLockAsync(shard.Id, request.WorkerId, request.UserId);
                
                if (!lockAcquired)
                {
                    // Someone else got it first, return null
                    return Ok((JobShard?)null);
                }
            }

            return Ok(shard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next available shard for worker {WorkerId}", request.WorkerId);
            return StatusCode(500, "An error occurred while getting next available shard");
        }
    }

    /// <summary>
    /// Release a shard lock (for worker services)
    /// </summary>
    [HttpPost("shards/{shardId}/release")]
    public async Task<ActionResult> ReleaseShard(int shardId, [FromBody] ReleaseShardRequest request)
    {
        try
        {
            var success = await _shardingService.ReleaseShardLockAsync(shardId, request.WorkerId);
            
            if (!success)
            {
                return BadRequest("Failed to release shard lock");
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing shard {ShardId} for worker {WorkerId}", shardId, request.WorkerId);
            return StatusCode(500, "An error occurred while releasing the shard");
        }
    }

    /// <summary>
    /// Cleanup expired shard locks (maintenance endpoint)
    /// </summary>
    [HttpPost("maintenance/cleanup-locks")]
    public async Task<ActionResult<int>> CleanupExpiredLocks()
    {
        try
        {
            var cleanedCount = await _shardingService.CleanupExpiredShardLocksAsync();
            
            _logger.LogInformation("Cleaned up {Count} expired shard locks", cleanedCount);
            
            return Ok(cleanedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired shard locks");
            return StatusCode(500, "An error occurred while cleaning up expired locks");
        }
    }
}

/// <summary>
/// Request models for shard operations
/// </summary>
public class RetryShardRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
}

public class UpdateShardProgressRequest
{
    public int ItemsProcessed { get; set; }
    public long BytesProcessed { get; set; }
}

public class CompleteShardRequest
{
    public bool IsSuccessful { get; set; }
    public int ActualItemCount { get; set; }
    public long ActualDataSizeBytes { get; set; }
    public string? ManifestHash { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GetNextShardRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public class ReleaseShardRequest
{
    public string WorkerId { get; set; } = string.Empty;
}
using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDiscoveryIntakeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChainOfCustodyController : ControllerBase
{
    private readonly IDbContextFactory<EDiscoveryDbContext> _dbContextFactory;
    private readonly IChainOfCustodyService _chainOfCustodyService;
    private readonly IComplianceLogger _complianceLogger;
    private readonly ILogger<ChainOfCustodyController> _logger;

    public ChainOfCustodyController(
        IDbContextFactory<EDiscoveryDbContext> dbContextFactory,
        IChainOfCustodyService chainOfCustodyService,
        IComplianceLogger complianceLogger,
        ILogger<ChainOfCustodyController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _chainOfCustodyService = chainOfCustodyService;
        _complianceLogger = complianceLogger;
        _logger = logger;
    }

    /// <summary>
    /// Generate manifest for a completed collection job
    /// </summary>
    [HttpPost("manifest/generate/{jobId}")]
    public async Task<ActionResult<JobManifest>> GenerateManifest(int jobId)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            // Load job with collected items
            var job = await dbContext.CollectionJobs
                .Include(j => j.CollectedItems)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return NotFound($"Collection job {jobId} not found");
            }

            if (job.Status != CollectionJobStatus.Completed)
            {
                return BadRequest($"Cannot generate manifest for job in status: {job.Status}");
            }

            // Check if manifest already exists
            var existingManifest = await dbContext.JobManifests
                .FirstOrDefaultAsync(m => m.JobId == jobId);

            if (existingManifest != null)
            {
                return Conflict($"Manifest already exists for job {jobId}. ManifestId: {existingManifest.ManifestId}");
            }

            // Generate the manifest
            var manifest = await _chainOfCustodyService.GenerateJobManifestAsync(job, job.CollectedItems, correlationId);

            // Save to database
            dbContext.JobManifests.Add(manifest);
            await dbContext.SaveChangesAsync();

            _complianceLogger.LogAudit("ManifestGeneratedViaAPI", new {
                JobId = jobId,
                ManifestId = manifest.ManifestId,
                TotalItems = manifest.TotalItems,
                RequestedBy = User.Identity?.Name ?? "Anonymous"
            }, correlationId: correlationId);

            return Ok(manifest);
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "Failed to generate manifest via API", new { JobId = jobId }, correlationId);
            return StatusCode(500, $"Failed to generate manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Seal a manifest with digital signature and immutable storage
    /// </summary>
    [HttpPost("manifest/seal/{manifestId}")]
    public async Task<ActionResult> SealManifest(int manifestId)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var manifest = await dbContext.JobManifests.FindAsync(manifestId);
            if (manifest == null)
            {
                return NotFound($"Manifest {manifestId} not found");
            }

            if (manifest.IsSealed)
            {
                return BadRequest($"Manifest {manifestId} is already sealed");
            }

            var success = await _chainOfCustodyService.SealManifestAsync(manifestId, correlationId);
            
            if (!success)
            {
                return StatusCode(500, "Failed to seal manifest");
            }

            _complianceLogger.LogAudit("ManifestSealedViaAPI", new {
                ManifestId = manifestId,
                RequestedBy = User.Identity?.Name ?? "Anonymous"
            }, correlationId: correlationId);

            return Ok(new { Message = "Manifest sealed successfully", ManifestId = manifestId });
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "Failed to seal manifest via API", new { ManifestId = manifestId }, correlationId);
            return StatusCode(500, $"Failed to seal manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Verify manifest integrity
    /// </summary>
    [HttpPost("manifest/verify/{manifestId}")]
    public async Task<ActionResult<ManifestVerification>> VerifyManifest(int manifestId)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var manifest = await dbContext.JobManifests.FindAsync(manifestId);
            if (manifest == null)
            {
                return NotFound($"Manifest {manifestId} not found");
            }

            var verification = await _chainOfCustodyService.VerifyManifestIntegrityAsync(manifestId, correlationId);

            // Save verification to database
            dbContext.ManifestVerifications.Add(verification);
            await dbContext.SaveChangesAsync();

            _complianceLogger.LogAudit("ManifestVerifiedViaAPI", new {
                ManifestId = manifestId,
                VerificationId = verification.VerificationId,
                Result = verification.Result.ToString(),
                RequestedBy = User.Identity?.Name ?? "Anonymous"
            }, correlationId: correlationId);

            return Ok(verification);
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "Failed to verify manifest via API", new { ManifestId = manifestId }, correlationId);
            return StatusCode(500, $"Failed to verify manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Get manifest details by ID
    /// </summary>
    [HttpGet("manifest/{manifestId}")]
    public async Task<ActionResult<JobManifest>> GetManifest(int manifestId)
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var manifest = await dbContext.JobManifests
                .Include(m => m.Job)
                .Include(m => m.Verifications)
                .FirstOrDefaultAsync(m => m.Id == manifestId);

            if (manifest == null)
            {
                return NotFound($"Manifest {manifestId} not found");
            }

            return Ok(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get manifest {ManifestId}", manifestId);
            return StatusCode(500, $"Failed to get manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all manifests for a job
    /// </summary>
    [HttpGet("job/{jobId}/manifests")]
    public async Task<ActionResult<IEnumerable<JobManifest>>> GetJobManifests(int jobId)
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var manifests = await dbContext.JobManifests
                .Where(m => m.JobId == jobId)
                .Include(m => m.Verifications)
                .OrderByDescending(m => m.CreatedDate)
                .ToListAsync();

            return Ok(manifests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get manifests for job {JobId}", jobId);
            return StatusCode(500, $"Failed to get job manifests: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate complete chain of custody for a job
    /// </summary>
    [HttpPost("job/{jobId}/validate")]
    public async Task<ActionResult<ChainOfCustodyValidationResult>> ValidateChainOfCustody(int jobId)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var job = await dbContext.CollectionJobs.FindAsync(jobId);
            if (job == null)
            {
                return NotFound($"Collection job {jobId} not found");
            }

            var validationResult = await _chainOfCustodyService.ValidateChainOfCustodyAsync(jobId, correlationId);

            _complianceLogger.LogAudit("ChainOfCustodyValidatedViaAPI", new {
                JobId = jobId,
                ValidationId = validationResult.ValidationId,
                IsValid = validationResult.IsValid,
                RequestedBy = User.Identity?.Name ?? "Anonymous"
            }, correlationId: correlationId);

            return Ok(validationResult);
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "Failed to validate chain of custody via API", new { JobId = jobId }, correlationId);
            return StatusCode(500, $"Failed to validate chain of custody: {ex.Message}");
        }
    }

    /// <summary>
    /// Get chain of custody summary for a matter
    /// </summary>
    [HttpGet("matter/{matterId}/summary")]
    public async Task<ActionResult<object>> GetMatterChainOfCustodySummary(int matterId)
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var summary = await dbContext.CollectionJobs
                .Where(j => j.MatterId == matterId)
                .GroupJoin(dbContext.JobManifests,
                    job => job.Id,
                    manifest => manifest.JobId,
                    (job, manifests) => new {
                        Job = job,
                        Manifests = manifests
                    })
                .Select(x => new {
                    JobId = x.Job.Id,
                    Custodian = x.Job.CustodianEmail,
                    Status = x.Job.Status.ToString(),
                    TotalItems = x.Job.ActualItemCount,
                    TotalSizeBytes = x.Job.ActualDataSizeBytes,
                    HasManifest = x.Manifests.Any(),
                    ManifestCount = x.Manifests.Count(),
                    ManifestSealed = x.Manifests.Any(m => m.IsSealed),
                    LastVerified = x.Manifests
                        .SelectMany(m => m.Verifications)
                        .Where(v => v.Result == VerificationResult.Valid)
                        .Max(v => (DateTime?)v.VerificationDate)
                })
                .ToListAsync();

            return Ok(new {
                MatterId = matterId,
                TotalJobs = summary.Count,
                JobsWithManifests = summary.Count(s => s.HasManifest),
                SealedManifests = summary.Count(s => s.ManifestSealed),
                TotalItems = summary.Sum(s => s.TotalItems),
                TotalSizeBytes = summary.Sum(s => s.TotalSizeBytes),
                Jobs = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chain of custody summary for matter {MatterId}", matterId);
            return StatusCode(500, $"Failed to get chain of custody summary: {ex.Message}");
        }
    }

    /// <summary>
    /// Download manifest file
    /// </summary>
    [HttpGet("manifest/{manifestId}/download")]
    public async Task<ActionResult> DownloadManifest(int manifestId, [FromQuery] string format = "json")
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            
            var manifest = await dbContext.JobManifests.FindAsync(manifestId);
            if (manifest == null)
            {
                return NotFound($"Manifest {manifestId} not found");
            }

            var filePath = manifest.FilePath;
            if (format.ToLower() == "csv")
            {
                // Try to find CSV version
                var csvPath = filePath.Replace(".json", ".csv");
                if (System.IO.File.Exists(csvPath))
                {
                    filePath = csvPath;
                }
            }

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Manifest file not found at {filePath}");
            }

            var fileName = Path.GetFileName(filePath);
            var contentType = format.ToLower() == "csv" ? "text/csv" : "application/json";
            
            _complianceLogger.LogAudit("ManifestDownloaded", new {
                ManifestId = manifestId,
                Format = format,
                FileName = fileName,
                RequestedBy = User.Identity?.Name ?? "Anonymous"
            });

            return PhysicalFile(Path.GetFullPath(filePath), contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download manifest {ManifestId}", manifestId);
            return StatusCode(500, $"Failed to download manifest: {ex.Message}");
        }
    }
}
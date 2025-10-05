using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using EDiscovery.Shared.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDiscoveryIntakeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MattersController : ControllerBase
{
    private readonly EDiscoveryDbContext _context;
    private readonly ILogger<MattersController> _logger;
    private readonly IComplianceLogger _complianceLogger;

    public MattersController(EDiscoveryDbContext context, ILogger<MattersController> logger, IComplianceLogger complianceLogger)
    {
        _context = context;
        _logger = logger;
        _complianceLogger = complianceLogger;
    }

    /// <summary>
    /// Get all active matters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Matter>>> GetMatters()
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("MattersController.GetMatters", correlationId);
        
        _logger.LogInformation("Retrieving all active matters | CorrelationId: {CorrelationId}", correlationId);
        
        try
        {
            var matters = await _context.Matters
                .Where(m => m.IsActive)
                .Include(m => m.CollectionJobs)
                .ToListAsync();
            
            _logger.LogInformation("Retrieved {MatterCount} active matters | CorrelationId: {CorrelationId}", 
                matters.Count, correlationId);
            
            _complianceLogger.LogAudit("MattersQueried", new { MatterCount = matters.Count }, null, correlationId);
            
            return matters;
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "MattersController.GetMatters", null, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Get a specific matter by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Matter>> GetMatter(int id)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("MattersController.GetMatter", correlationId);
        
        _logger.LogInformation("Retrieving matter with ID: {MatterId} | CorrelationId: {CorrelationId}", id, correlationId);
        
        try
        {
            var matter = await _context.Matters
                .Include(m => m.CollectionJobs)
                .ThenInclude(j => j.CollectedItems)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (matter == null)
            {
                _logger.LogWarning("Matter not found with ID: {MatterId} | CorrelationId: {CorrelationId}", id, correlationId);
                return NotFound();
            }

            _logger.LogInformation("Retrieved matter: {MatterId} - {MatterName} | CorrelationId: {CorrelationId}", 
                matter.Id, matter.Name, correlationId);
            
            _complianceLogger.LogAudit("MatterAccessed", new { MatterId = matter.Id, MatterName = matter.Name }, null, correlationId);

            return matter;
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "MattersController.GetMatter", new { MatterId = id }, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Create a new matter
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Matter>> PostMatter(Matter matter)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("MattersController.PostMatter", correlationId);
        
        _logger.LogInformation("Creating new matter: {MatterName} | CorrelationId: {CorrelationId}", 
            matter.Name, correlationId);
        
        try
        {
            matter.CreatedDate = DateTime.UtcNow;
            _context.Matters.Add(matter);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new matter: {MatterId} - {MatterName} | CorrelationId: {CorrelationId}", 
                matter.Id, matter.Name, correlationId);
            
            _complianceLogger.LogAudit("MatterCreated", new 
            { 
                MatterId = matter.Id, 
                MatterName = matter.Name,
                CaseNumber = matter.CaseNumber,
                IsActive = matter.IsActive
            }, null, correlationId);

            return CreatedAtAction("GetMatter", new { id = matter.Id }, matter);
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "MattersController.PostMatter", new { MatterName = matter.Name }, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Update an existing matter
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> PutMatter(int id, Matter matter)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("MattersController.PutMatter", correlationId);
        
        _logger.LogInformation("Updating matter: {MatterId} | CorrelationId: {CorrelationId}", id, correlationId);
        
        if (id != matter.Id)
        {
            _logger.LogWarning("Matter ID mismatch. URL ID: {UrlId}, Matter ID: {MatterId} | CorrelationId: {CorrelationId}", 
                id, matter.Id, correlationId);
            return BadRequest();
        }

        try
        {
            _context.Entry(matter).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated matter: {MatterId} - {MatterName} | CorrelationId: {CorrelationId}", 
                id, matter.Name, correlationId);
            
            _complianceLogger.LogAudit("MatterUpdated", new 
            { 
                MatterId = matter.Id, 
                MatterName = matter.Name,
                IsActive = matter.IsActive
            }, null, correlationId);
            
            return NoContent();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!MatterExists(id))
            {
                _logger.LogWarning("Matter not found during update: {MatterId} | CorrelationId: {CorrelationId}", 
                    id, correlationId);
                return NotFound();
            }
            else
            {
                _complianceLogger.LogError(ex, "MattersController.PutMatter.ConcurrencyException", 
                    new { MatterId = id }, correlationId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "MattersController.PutMatter", new { MatterId = id }, correlationId);
            throw;
        }
    }

    /// <summary>
    /// Deactivate a matter (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMatter(int id)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        using var performanceTimer = _complianceLogger.StartPerformanceTimer("MattersController.DeleteMatter", correlationId);
        
        _logger.LogInformation("Deactivating matter: {MatterId} | CorrelationId: {CorrelationId}", id, correlationId);
        
        try
        {
            var matter = await _context.Matters.FindAsync(id);
            if (matter == null)
            {
                _logger.LogWarning("Matter not found for deletion: {MatterId} | CorrelationId: {CorrelationId}", 
                    id, correlationId);
                return NotFound();
            }

            matter.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deactivated matter: {MatterId} - {MatterName} | CorrelationId: {CorrelationId}", 
                id, matter.Name, correlationId);
            
            _complianceLogger.LogAudit("MatterDeactivated", new 
            { 
                MatterId = matter.Id, 
                MatterName = matter.Name
            }, null, correlationId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "MattersController.DeleteMatter", new { MatterId = id }, correlationId);
            throw;
        }
    }

    private bool MatterExists(int id)
    {
        return _context.Matters.Any(e => e.Id == id);
    }
}
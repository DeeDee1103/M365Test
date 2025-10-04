using EDiscovery.Shared.Models;
using EDiscoveryIntakeApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDiscoveryIntakeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MattersController : ControllerBase
{
    private readonly EDiscoveryDbContext _context;
    private readonly ILogger<MattersController> _logger;

    public MattersController(EDiscoveryDbContext context, ILogger<MattersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all active matters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Matter>>> GetMatters()
    {
        return await _context.Matters
            .Where(m => m.IsActive)
            .Include(m => m.CollectionJobs)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific matter by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Matter>> GetMatter(int id)
    {
        var matter = await _context.Matters
            .Include(m => m.CollectionJobs)
            .ThenInclude(j => j.CollectedItems)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matter == null)
        {
            return NotFound();
        }

        return matter;
    }

    /// <summary>
    /// Create a new matter
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Matter>> PostMatter(Matter matter)
    {
        matter.CreatedDate = DateTime.UtcNow;
        _context.Matters.Add(matter);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new matter: {MatterId} - {MatterName}", matter.Id, matter.Name);

        return CreatedAtAction("GetMatter", new { id = matter.Id }, matter);
    }

    /// <summary>
    /// Update an existing matter
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> PutMatter(int id, Matter matter)
    {
        if (id != matter.Id)
        {
            return BadRequest();
        }

        _context.Entry(matter).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated matter: {MatterId}", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!MatterExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    /// <summary>
    /// Deactivate a matter (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMatter(int id)
    {
        var matter = await _context.Matters.FindAsync(id);
        if (matter == null)
        {
            return NotFound();
        }

        matter.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated matter: {MatterId}", id);

        return NoContent();
    }

    private bool MatterExists(int id)
    {
        return _context.Matters.Any(e => e.Id == id);
    }
}
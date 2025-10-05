using EDiscovery.Shared.Models;
using EDiscovery.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Interface for managing delta queries and cursor storage
/// </summary>
public interface IDeltaQueryService
{
    /// <summary>
    /// Initialize delta tracking for a custodian and data type
    /// </summary>
    Task<DeltaCursor> InitializeDeltaTrackingAsync(string custodianEmail, DeltaType deltaType, int? collectionJobId = null);

    /// <summary>
    /// Get the current delta cursor for a custodian and data type
    /// </summary>
    Task<DeltaCursor?> GetDeltaCursorAsync(string custodianEmail, DeltaType deltaType);

    /// <summary>
    /// Update delta cursor with new token and statistics
    /// </summary>
    Task UpdateDeltaCursorAsync(string scopeId, string newDeltaToken, int itemCount, long sizeBytes);

    /// <summary>
    /// Perform a delta query for mail items
    /// </summary>
    Task<DeltaQueryResult> QueryMailDeltaAsync(string custodianEmail, DeltaCursor cursor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform a delta query for OneDrive items  
    /// </summary>
    Task<DeltaQueryResult> QueryOneDriveDeltaAsync(string custodianEmail, DeltaCursor cursor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if delta tracking should be used for a collection
    /// </summary>
    Task<bool> ShouldUseDeltaQueryAsync(string custodianEmail, DeltaType deltaType);

    /// <summary>
    /// Force a full resync by removing delta cursor
    /// </summary>
    Task ResetDeltaTrackingAsync(string custodianEmail, DeltaType deltaType);

    /// <summary>
    /// Get all active delta cursors
    /// </summary>
    Task<List<DeltaCursor>> GetActiveDeltaCursorsAsync();

    /// <summary>
    /// Clean up old or failed delta cursors
    /// </summary>
    Task CleanupStaleDeltaCursorsAsync();
}

/// <summary>
/// Service for managing Microsoft Graph delta queries and cursor persistence
/// </summary>
public class DeltaQueryService : IDeltaQueryService
{
    private readonly ILogger<DeltaQueryService> _logger;
    private readonly IComplianceLogger _complianceLogger;
    private readonly EDiscoveryDbContext _dbContext;
    private readonly DeltaQueryOptions _options;

    public DeltaQueryService(
        ILogger<DeltaQueryService> logger,
        IComplianceLogger complianceLogger,
        EDiscoveryDbContext dbContext,
        IOptions<DeltaQueryOptions> options)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<DeltaCursor> InitializeDeltaTrackingAsync(string custodianEmail, DeltaType deltaType, int? collectionJobId = null)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        var scopeId = GenerateScopeId(custodianEmail, deltaType);

        _logger.LogInformation("Initializing delta tracking for {ScopeId} | CorrelationId: {CorrelationId}", scopeId, correlationId);

        // Check if cursor already exists
        var existingCursor = await GetDeltaCursorAsync(custodianEmail, deltaType);
        if (existingCursor != null)
        {
            _logger.LogInformation("Delta cursor already exists for {ScopeId}, updating active status | CorrelationId: {CorrelationId}", scopeId, correlationId);
            existingCursor.IsActive = true;
            existingCursor.UpdatedDate = DateTime.UtcNow;
            existingCursor.ErrorMessage = null;
            
            await _dbContext.SaveChangesAsync();
            return existingCursor;
        }

        // Create new delta cursor with initial empty token (will trigger baseline collection)
        var cursor = new DeltaCursor
        {
            ScopeId = scopeId,
            DeltaType = deltaType,
            CustodianEmail = custodianEmail,
            DeltaToken = string.Empty, // Empty token indicates baseline collection needed
            LastDeltaTime = DateTime.UtcNow,
            CollectionJobId = collectionJobId,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _dbContext.DeltaCursors.Add(cursor);
        await _dbContext.SaveChangesAsync();

        _complianceLogger.LogAudit("DeltaTrackingInitialized", new
        {
            ScopeId = scopeId,
            DeltaType = deltaType.ToString(),
            CustodianEmail = custodianEmail,
            CollectionJobId = collectionJobId
        }, custodianEmail, correlationId);

        _logger.LogInformation("Delta tracking initialized for {ScopeId} with ID {CursorId} | CorrelationId: {CorrelationId}", 
            scopeId, cursor.Id, correlationId);

        return cursor;
    }

    public async Task<DeltaCursor?> GetDeltaCursorAsync(string custodianEmail, DeltaType deltaType)
    {
        var scopeId = GenerateScopeId(custodianEmail, deltaType);
        
        return await _dbContext.DeltaCursors
            .Where(c => c.ScopeId == scopeId && c.DeltaType == deltaType && c.IsActive)
            .OrderByDescending(c => c.UpdatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateDeltaCursorAsync(string scopeId, string newDeltaToken, int itemCount, long sizeBytes)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        var cursor = await _dbContext.DeltaCursors
            .Where(c => c.ScopeId == scopeId && c.IsActive)
            .FirstOrDefaultAsync();

        if (cursor == null)
        {
            _logger.LogWarning("Delta cursor not found for scope {ScopeId} | CorrelationId: {CorrelationId}", scopeId, correlationId);
            return;
        }

        // Update cursor with new delta token and statistics
        cursor.DeltaToken = newDeltaToken;
        cursor.LastDeltaTime = DateTime.UtcNow;
        cursor.LastDeltaItemCount = itemCount;
        cursor.LastDeltaSizeBytes = sizeBytes;
        cursor.DeltaQueryCount++;
        cursor.UpdatedDate = DateTime.UtcNow;
        cursor.ErrorMessage = null;

        // Mark baseline as completed if this is the first successful delta with items
        if (cursor.BaselineCompletedAt == null && !string.IsNullOrEmpty(newDeltaToken))
        {
            cursor.BaselineCompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Baseline collection marked complete for {ScopeId} | CorrelationId: {CorrelationId}", scopeId, correlationId);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Delta cursor updated for {ScopeId}: {ItemCount} items, {SizeBytes} bytes | CorrelationId: {CorrelationId}", 
            scopeId, itemCount, sizeBytes, correlationId);
    }

    public async Task<DeltaQueryResult> QueryMailDeltaAsync(string custodianEmail, DeltaCursor cursor, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting mail delta query for {CustodianEmail} | CorrelationId: {CorrelationId}", custodianEmail, correlationId);

        try
        {
            // In POC: Simulate delta query with mock data
            // In production: Call Microsoft Graph API with delta token
            await Task.Delay(100, cancellationToken); // Simulate API call

            var result = new DeltaQueryResult
            {
                ItemCount = 5, // Simulated incremental changes
                TotalSizeBytes = 1024 * 1024, // 1MB
                NextDeltaToken = $"delta-token-{DateTime.UtcNow:yyyyMMddHHmmss}",
                HasMoreResults = false,
                QueryDuration = stopwatch.Elapsed,
                IsSuccessful = true
            };

            // Simulate some collected mail items
            for (int i = 0; i < result.ItemCount; i++)
            {
                result.Items.Add(new CollectedItem
                {
                    ItemId = $"mail-delta-{custodianEmail}-{i}",
                    ItemType = "Email",
                    Subject = $"Delta Mail Item {i}",
                    From = "sender@company.com",
                    To = custodianEmail,
                    ItemDate = DateTime.UtcNow.AddHours(-i),
                    CollectedDate = DateTime.UtcNow,
                    SizeBytes = 1024 * 200, // 200KB per item
                    Sha256Hash = $"sha256-hash-{Guid.NewGuid()}",
                    IsSuccessful = true
                });
            }

            _complianceLogger.LogAudit("MailDeltaQueryCompleted", new
            {
                CustodianEmail = custodianEmail,
                ItemCount = result.ItemCount,
                SizeBytes = result.TotalSizeBytes,
                Duration = result.QueryDuration.TotalMilliseconds
            }, custodianEmail, correlationId);

            _logger.LogInformation("Mail delta query completed for {CustodianEmail}: {ItemCount} items in {Duration}ms | CorrelationId: {CorrelationId}", 
                custodianEmail, result.ItemCount, result.QueryDuration.TotalMilliseconds, correlationId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mail delta query failed for {CustodianEmail} | CorrelationId: {CorrelationId}", custodianEmail, correlationId);
            
            return new DeltaQueryResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                QueryDuration = stopwatch.Elapsed
            };
        }
    }

    public async Task<DeltaQueryResult> QueryOneDriveDeltaAsync(string custodianEmail, DeltaCursor cursor, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting OneDrive delta query for {CustodianEmail} | CorrelationId: {CorrelationId}", custodianEmail, correlationId);

        try
        {
            // In POC: Simulate delta query with mock data
            // In production: Call Microsoft Graph API with delta token
            await Task.Delay(150, cancellationToken); // Simulate API call

            var result = new DeltaQueryResult
            {
                ItemCount = 3, // Simulated incremental changes
                TotalSizeBytes = 5 * 1024 * 1024, // 5MB
                NextDeltaToken = $"onedrive-delta-token-{DateTime.UtcNow:yyyyMMddHHmmss}",
                HasMoreResults = false,
                QueryDuration = stopwatch.Elapsed,
                IsSuccessful = true
            };

            // Simulate some collected OneDrive items
            for (int i = 0; i < result.ItemCount; i++)
            {
                result.Items.Add(new CollectedItem
                {
                    ItemId = $"onedrive-delta-{custodianEmail}-{i}",
                    ItemType = "File",
                    Subject = $"Document-{i}.docx",
                    From = custodianEmail,
                    ItemDate = DateTime.UtcNow.AddHours(-i),
                    CollectedDate = DateTime.UtcNow,
                    SizeBytes = (long)(1024 * 1024 * 1.5), // 1.5MB per file
                    Sha256Hash = $"sha256-hash-{Guid.NewGuid()}",
                    FilePath = $"/onedrive/{custodianEmail}/Document-{i}.docx",
                    IsSuccessful = true
                });
            }

            _complianceLogger.LogAudit("OneDriveDeltaQueryCompleted", new
            {
                CustodianEmail = custodianEmail,
                ItemCount = result.ItemCount,
                SizeBytes = result.TotalSizeBytes,
                Duration = result.QueryDuration.TotalMilliseconds
            }, custodianEmail, correlationId);

            _logger.LogInformation("OneDrive delta query completed for {CustodianEmail}: {ItemCount} items in {Duration}ms | CorrelationId: {CorrelationId}", 
                custodianEmail, result.ItemCount, result.QueryDuration.TotalMilliseconds, correlationId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OneDrive delta query failed for {CustodianEmail} | CorrelationId: {CorrelationId}", custodianEmail, correlationId);
            
            return new DeltaQueryResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                QueryDuration = stopwatch.Elapsed
            };
        }
    }

    public async Task<bool> ShouldUseDeltaQueryAsync(string custodianEmail, DeltaType deltaType)
    {
        // Check if delta queries are enabled for this type
        bool isEnabled = deltaType switch
        {
            DeltaType.Mail => _options.EnableMailDelta,
            DeltaType.OneDrive => _options.EnableOneDriveDelta,
            _ => false
        };

        if (!isEnabled)
        {
            _logger.LogDebug("Delta queries disabled for {DeltaType}", deltaType);
            return false;
        }

        // Check if we have a valid, recent delta cursor
        var cursor = await GetDeltaCursorAsync(custodianEmail, deltaType);
        if (cursor == null)
        {
            _logger.LogDebug("No delta cursor found for {CustodianEmail}, {DeltaType}", custodianEmail, deltaType);
            return false;
        }

        // Check if cursor is too old
        var maxAge = TimeSpan.FromDays(_options.MaxDeltaAgeDays);
        if (DateTime.UtcNow - cursor.LastDeltaTime > maxAge)
        {
            _logger.LogInformation("Delta cursor too old for {CustodianEmail}, {DeltaType} (age: {Age})", 
                custodianEmail, deltaType, DateTime.UtcNow - cursor.LastDeltaTime);
            return false;
        }

        return true;
    }

    public async Task ResetDeltaTrackingAsync(string custodianEmail, DeltaType deltaType)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        var scopeId = GenerateScopeId(custodianEmail, deltaType);

        _logger.LogInformation("Resetting delta tracking for {ScopeId} | CorrelationId: {CorrelationId}", scopeId, correlationId);

        var cursor = await GetDeltaCursorAsync(custodianEmail, deltaType);
        if (cursor != null)
        {
            cursor.IsActive = false;
            cursor.UpdatedDate = DateTime.UtcNow;
            cursor.ErrorMessage = "Reset by user/system";
            
            await _dbContext.SaveChangesAsync();

            _complianceLogger.LogAudit("DeltaTrackingReset", new
            {
                ScopeId = scopeId,
                DeltaType = deltaType.ToString(),
                CustodianEmail = custodianEmail
            }, custodianEmail, correlationId);
        }

        _logger.LogInformation("Delta tracking reset for {ScopeId} | CorrelationId: {CorrelationId}", scopeId, correlationId);
    }

    public async Task<List<DeltaCursor>> GetActiveDeltaCursorsAsync()
    {
        return await _dbContext.DeltaCursors
            .Where(c => c.IsActive)
            .OrderBy(c => c.LastDeltaTime)
            .ToListAsync();
    }

    public async Task CleanupStaleDeltaCursorsAsync()
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        var cutoffDate = DateTime.UtcNow.AddDays(-_options.MaxDeltaAgeDays);

        _logger.LogInformation("Cleaning up stale delta cursors older than {CutoffDate} | CorrelationId: {CorrelationId}", cutoffDate, correlationId);

        var staleCursors = await _dbContext.DeltaCursors
            .Where(c => c.IsActive && c.LastDeltaTime < cutoffDate)
            .ToListAsync();

        foreach (var cursor in staleCursors)
        {
            cursor.IsActive = false;
            cursor.UpdatedDate = DateTime.UtcNow;
            cursor.ErrorMessage = "Marked stale during cleanup";
        }

        if (staleCursors.Any())
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Marked {Count} delta cursors as stale | CorrelationId: {CorrelationId}", staleCursors.Count, correlationId);
        }
    }

    private static string GenerateScopeId(string custodianEmail, DeltaType deltaType)
    {
        return $"{deltaType.ToString().ToLower()}:{custodianEmail.ToLower()}";
    }
}
using System.Text.Json;
using EDiscovery.Shared.Models;
using Microsoft.Extensions.Logging;

namespace EDiscovery.Shared.Services;

/// <summary>
/// File-based delta cursor storage service for ./state/cursors.json
/// </summary>
public interface IFileDeltaCursorStorage
{
    /// <summary>
    /// Load all delta cursors from file
    /// </summary>
    Task<Dictionary<string, DeltaCursor>> LoadCursorsAsync();

    /// <summary>
    /// Save all delta cursors to file
    /// </summary>
    Task SaveCursorsAsync(Dictionary<string, DeltaCursor> cursors);

    /// <summary>
    /// Get a specific cursor by scope
    /// </summary>
    Task<DeltaCursor?> GetCursorAsync(string scopeId);

    /// <summary>
    /// Update a specific cursor
    /// </summary>
    Task UpdateCursorAsync(string scopeId, DeltaCursor cursor);

    /// <summary>
    /// Remove a cursor
    /// </summary>
    Task RemoveCursorAsync(string scopeId);
}

/// <summary>
/// File-based implementation of delta cursor storage
/// </summary>
public class FileDeltaCursorStorage : IFileDeltaCursorStorage
{
    private readonly string _filePath;
    private readonly ILogger<FileDeltaCursorStorage> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public FileDeltaCursorStorage(ILogger<FileDeltaCursorStorage> logger)
    {
        _logger = logger;
        
        // Ensure state directory exists
        var stateDir = Path.Combine(Directory.GetCurrentDirectory(), "state");
        Directory.CreateDirectory(stateDir);
        
        _filePath = Path.Combine(stateDir, "cursors.json");
        _logger.LogInformation("Delta cursor storage initialized at: {FilePath}", _filePath);
    }

    public async Task<Dictionary<string, DeltaCursor>> LoadCursorsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Cursor file not found, returning empty dictionary");
                return new Dictionary<string, DeltaCursor>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, DeltaCursor>();
            }

            var cursors = JsonSerializer.Deserialize<Dictionary<string, DeltaCursor>>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogInformation("Loaded {Count} delta cursors from file", cursors?.Count ?? 0);
            return cursors ?? new Dictionary<string, DeltaCursor>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading delta cursors from file");
            return new Dictionary<string, DeltaCursor>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveCursorsAsync(Dictionary<string, DeltaCursor> cursors)
    {
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(cursors, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, json);
            _logger.LogInformation("Saved {Count} delta cursors to file", cursors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving delta cursors to file");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<DeltaCursor?> GetCursorAsync(string scopeId)
    {
        var cursors = await LoadCursorsAsync();
        cursors.TryGetValue(scopeId, out var cursor);
        return cursor;
    }

    public async Task UpdateCursorAsync(string scopeId, DeltaCursor cursor)
    {
        var cursors = await LoadCursorsAsync();
        cursors[scopeId] = cursor;
        await SaveCursorsAsync(cursors);
    }

    public async Task RemoveCursorAsync(string scopeId)
    {
        var cursors = await LoadCursorsAsync();
        if (cursors.Remove(scopeId))
        {
            await SaveCursorsAsync(cursors);
            _logger.LogInformation("Removed delta cursor for scope: {ScopeId}", scopeId);
        }
    }
}
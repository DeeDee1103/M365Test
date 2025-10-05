using EDiscovery.Shared.Models;
using System.Text.Json;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Service for managing job shard checkpoints to enable idempotent restarts
/// </summary>
public interface IShardCheckpointService
{
    /// <summary>
    /// Creates a checkpoint for mail collection progress
    /// </summary>
    Task<JobShardCheckpoint> CreateMailFolderCheckpointAsync(int shardId, string folderId, string folderName, 
        string? deltaToken, int itemsProcessed, string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a checkpoint for OneDrive collection progress
    /// </summary>
    Task<JobShardCheckpoint> CreateOneDriveCheckpointAsync(int shardId, string driveId, string? deltaToken, 
        int itemsProcessed, string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a checkpoint for SharePoint collection progress
    /// </summary>
    Task<JobShardCheckpoint> CreateSharePointCheckpointAsync(int shardId, string siteId, string listId, 
        string? deltaToken, int itemsProcessed, string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a checkpoint for Teams collection progress
    /// </summary>
    Task<JobShardCheckpoint> CreateTeamsCheckpointAsync(int shardId, string teamId, string channelId, 
        string? deltaToken, int itemsProcessed, string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the last completed checkpoint for a specific type and shard
    /// </summary>
    Task<JobShardCheckpoint?> GetLastCheckpointAsync(int shardId, string checkpointType, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all incomplete checkpoints for resuming collection
    /// </summary>
    Task<List<JobShardCheckpoint>> GetResumeCheckpointsAsync(int shardId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks a checkpoint as completed with final statistics
    /// </summary>
    Task<bool> CompleteCheckpointAsync(int checkpointId, int totalItemsProcessed, long totalBytesProcessed, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a batch checkpoint for processing multiple items together
    /// </summary>
    Task<JobShardCheckpoint> CreateBatchCheckpointAsync(int shardId, string batchType, int batchSize, 
        object batchContext, string correlationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates checkpoint integrity for restart safety
    /// </summary>
    Task<CheckpointValidationResult> ValidateCheckpointIntegrityAsync(int shardId, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of shard checkpoint service
/// </summary>
public class ShardCheckpointService : IShardCheckpointService
{
    private readonly IJobShardingService _shardingService;
    private readonly ILogger<ShardCheckpointService> _logger;

    public ShardCheckpointService(
        IJobShardingService shardingService,
        ILogger<ShardCheckpointService> logger)
    {
        _shardingService = shardingService;
        _logger = logger;
    }

    public async Task<JobShardCheckpoint> CreateMailFolderCheckpointAsync(int shardId, string folderId, string folderName, 
        string? deltaToken, int itemsProcessed, string correlationId, CancellationToken cancellationToken = default)
    {
        var checkpointData = new MailFolderCheckpointData
        {
            FolderId = folderId,
            FolderName = folderName,
            DeltaToken = deltaToken,
            LastProcessedTime = DateTime.UtcNow,
            ItemsProcessedInFolder = itemsProcessed
        };

        var checkpointKey = $"mail_folder_{folderId}";
        var jsonData = JsonSerializer.Serialize(checkpointData);

        return await _shardingService.CreateCheckpointAsync(
            shardId, "MailFolderProgress", checkpointKey, jsonData, correlationId, cancellationToken);
    }

    public async Task<JobShardCheckpoint> CreateOneDriveCheckpointAsync(int shardId, string driveId, string? deltaToken, 
        int itemsProcessed, string correlationId, CancellationToken cancellationToken = default)
    {
        var checkpointData = new OneDriveCheckpointData
        {
            DriveId = driveId,
            DeltaToken = deltaToken,
            LastProcessedTime = DateTime.UtcNow,
            ItemsProcessedInDrive = itemsProcessed
        };

        var checkpointKey = $"onedrive_{driveId}";
        var jsonData = JsonSerializer.Serialize(checkpointData);

        return await _shardingService.CreateCheckpointAsync(
            shardId, "OneDriveProgress", checkpointKey, jsonData, correlationId, cancellationToken);
    }

    public async Task<JobShardCheckpoint> CreateSharePointCheckpointAsync(int shardId, string siteId, string listId, 
        string? deltaToken, int itemsProcessed, string correlationId, CancellationToken cancellationToken = default)
    {
        var checkpointData = new SharePointCheckpointData
        {
            SiteId = siteId,
            ListId = listId,
            DeltaToken = deltaToken,
            LastProcessedTime = DateTime.UtcNow,
            ItemsProcessedInList = itemsProcessed
        };

        var checkpointKey = $"sharepoint_{siteId}_{listId}";
        var jsonData = JsonSerializer.Serialize(checkpointData);

        return await _shardingService.CreateCheckpointAsync(
            shardId, "SharePointProgress", checkpointKey, jsonData, correlationId, cancellationToken);
    }

    public async Task<JobShardCheckpoint> CreateTeamsCheckpointAsync(int shardId, string teamId, string channelId, 
        string? deltaToken, int itemsProcessed, string correlationId, CancellationToken cancellationToken = default)
    {
        var checkpointData = new TeamsCheckpointData
        {
            TeamId = teamId,
            ChannelId = channelId,
            DeltaToken = deltaToken,
            LastProcessedTime = DateTime.UtcNow,
            ItemsProcessedInChannel = itemsProcessed
        };

        var checkpointKey = $"teams_{teamId}_{channelId}";
        var jsonData = JsonSerializer.Serialize(checkpointData);

        return await _shardingService.CreateCheckpointAsync(
            shardId, "TeamsProgress", checkpointKey, jsonData, correlationId, cancellationToken);
    }

    public async Task<JobShardCheckpoint?> GetLastCheckpointAsync(int shardId, string checkpointType, 
        CancellationToken cancellationToken = default)
    {
        var checkpoints = await _shardingService.GetShardCheckpointsAsync(shardId, cancellationToken);
        
        return checkpoints
            .Where(c => c.CheckpointType == checkpointType && c.IsCompleted)
            .OrderByDescending(c => c.CreatedDate)
            .FirstOrDefault();
    }

    public async Task<List<JobShardCheckpoint>> GetResumeCheckpointsAsync(int shardId, 
        CancellationToken cancellationToken = default)
    {
        var incompleteCheckpoints = await _shardingService.GetIncompleteCheckpointsAsync(shardId, cancellationToken);
        
        _logger.LogInformation("Found {Count} incomplete checkpoints for shard {ShardId} resume", 
            incompleteCheckpoints.Count, shardId);
        
        return incompleteCheckpoints;
    }

    public async Task<bool> CompleteCheckpointAsync(int checkpointId, int totalItemsProcessed, long totalBytesProcessed, 
        CancellationToken cancellationToken = default)
    {
        var result = await _shardingService.CompleteCheckpointAsync(
            checkpointId, totalItemsProcessed, totalBytesProcessed, cancellationToken);
        
        if (result)
        {
            _logger.LogDebug("Completed checkpoint {CheckpointId} with {Items} items and {Bytes} bytes", 
                checkpointId, totalItemsProcessed, totalBytesProcessed);
        }
        
        return result;
    }

    public async Task<JobShardCheckpoint> CreateBatchCheckpointAsync(int shardId, string batchType, int batchSize, 
        object batchContext, string correlationId, CancellationToken cancellationToken = default)
    {
        var checkpointData = new BatchCheckpointData
        {
            BatchType = batchType,
            BatchSize = batchSize,
            BatchContext = JsonSerializer.Serialize(batchContext),
            LastProcessedTime = DateTime.UtcNow
        };

        var checkpointKey = $"batch_{batchType}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var jsonData = JsonSerializer.Serialize(checkpointData);

        return await _shardingService.CreateCheckpointAsync(
            shardId, "BatchProgress", checkpointKey, jsonData, correlationId, cancellationToken);
    }

    public async Task<CheckpointValidationResult> ValidateCheckpointIntegrityAsync(int shardId, 
        CancellationToken cancellationToken = default)
    {
        var checkpoints = await _shardingService.GetShardCheckpointsAsync(shardId, cancellationToken);
        
        var result = new CheckpointValidationResult
        {
            ShardId = shardId,
            TotalCheckpoints = checkpoints.Count,
            CompletedCheckpoints = checkpoints.Count(c => c.IsCompleted),
            IncompleteCheckpoints = checkpoints.Count(c => !c.IsCompleted),
            IsValid = true,
            ValidationErrors = new List<string>()
        };

        // Validate checkpoint sequence and integrity
        var groupedCheckpoints = checkpoints.GroupBy(c => c.CheckpointType);
        
        foreach (var group in groupedCheckpoints)
        {
            var orderedCheckpoints = group.OrderBy(c => c.CreatedDate).ToList();
            
            // Check for gaps in checkpoint sequence
            for (int i = 1; i < orderedCheckpoints.Count; i++)
            {
                var prev = orderedCheckpoints[i - 1];
                var current = orderedCheckpoints[i];
                
                if (!prev.IsCompleted && current.IsCompleted)
                {
                    result.ValidationErrors.Add(
                        $"Checkpoint sequence error in {group.Key}: incomplete checkpoint {prev.Id} followed by completed {current.Id}");
                    result.IsValid = false;
                }
            }
        }

        _logger.LogDebug("Validated {Total} checkpoints for shard {ShardId} | Valid: {IsValid} | Errors: {ErrorCount}", 
            result.TotalCheckpoints, shardId, result.IsValid, result.ValidationErrors.Count);

        return result;
    }
}

/// <summary>
/// Checkpoint data models for different collection types
/// </summary>
public class MailFolderCheckpointData
{
    public string FolderId { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string? DeltaToken { get; set; }
    public DateTime LastProcessedTime { get; set; }
    public int ItemsProcessedInFolder { get; set; }
    public List<string> ProcessedMessageIds { get; set; } = new();
}

public class OneDriveCheckpointData
{
    public string DriveId { get; set; } = string.Empty;
    public string? DeltaToken { get; set; }
    public DateTime LastProcessedTime { get; set; }
    public int ItemsProcessedInDrive { get; set; }
    public List<string> ProcessedFileIds { get; set; } = new();
    public string? LastProcessedFolderId { get; set; }
}

public class SharePointCheckpointData
{
    public string SiteId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public string? DeltaToken { get; set; }
    public DateTime LastProcessedTime { get; set; }
    public int ItemsProcessedInList { get; set; }
    public List<string> ProcessedItemIds { get; set; } = new();
}

public class TeamsCheckpointData
{
    public string TeamId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string? DeltaToken { get; set; }
    public DateTime LastProcessedTime { get; set; }
    public int ItemsProcessedInChannel { get; set; }
    public List<string> ProcessedMessageIds { get; set; } = new();
}

public class BatchCheckpointData
{
    public string BatchType { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public string BatchContext { get; set; } = string.Empty;
    public DateTime LastProcessedTime { get; set; }
}

/// <summary>
/// Result of checkpoint validation
/// </summary>
public class CheckpointValidationResult
{
    public int ShardId { get; set; }
    public int TotalCheckpoints { get; set; }
    public int CompletedCheckpoints { get; set; }
    public int IncompleteCheckpoints { get; set; }
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}
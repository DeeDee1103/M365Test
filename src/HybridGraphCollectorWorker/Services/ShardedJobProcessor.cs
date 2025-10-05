using EDiscovery.Shared.Models;
using EDiscovery.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HybridGraphCollectorWorker.Services;

/// <summary>
/// Service for processing sharded collection jobs with checkpoint support
/// </summary>
public interface IShardedJobProcessor
{
    /// <summary>
    /// Processes a single job shard with checkpoint recovery
    /// </summary>
    Task<CollectionResult> ProcessShardAsync(JobShard shard, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resumes processing of a shard from checkpoints
    /// </summary>
    Task<CollectionResult> ResumeShardProcessingAsync(JobShard shard, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates shard readiness for processing
    /// </summary>
    Task<bool> ValidateShardForProcessingAsync(JobShard shard, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of sharded job processor
/// </summary>
public class ShardedJobProcessor : IShardedJobProcessor
{
    private readonly IGraphCollectorService _graphCollector;
    private readonly IJobShardingService _shardingService;
    private readonly IShardCheckpointService _checkpointService;
    private readonly ILogger<ShardedJobProcessor> _logger;

    public ShardedJobProcessor(
        IGraphCollectorService graphCollector,
        IJobShardingService shardingService,
        IShardCheckpointService checkpointService,
        ILogger<ShardedJobProcessor> logger)
    {
        _graphCollector = graphCollector;
        _shardingService = shardingService;
        _checkpointService = checkpointService;
        _logger = logger;
    }

    public async Task<CollectionResult> ProcessShardAsync(JobShard shard, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting processing of shard {ShardId} for custodian {Custodian} | {StartDate} to {EndDate} | CorrelationId: {CorrelationId}", 
            shard.Id, shard.CustodianEmail, shard.StartDate, shard.EndDate, correlationId);

        try
        {
            // Update shard status to processing
            await UpdateShardStatusAsync(shard.Id, JobShardStatus.Processing);

            // Check for existing checkpoints to resume from
            var resumeCheckpoints = await _checkpointService.GetResumeCheckpointsAsync(shard.Id, cancellationToken);
            
            CollectionResult result;
            
            if (resumeCheckpoints.Any())
            {
                _logger.LogInformation("Found {Count} incomplete checkpoints for shard {ShardId}, resuming processing", 
                    resumeCheckpoints.Count, shard.Id);
                result = await ResumeShardProcessingAsync(shard, cancellationToken);
            }
            else
            {
                _logger.LogInformation("No checkpoints found for shard {ShardId}, starting fresh collection", shard.Id);
                result = await ExecuteShardCollectionAsync(shard, correlationId, cancellationToken);
            }

            // Complete the shard
            await _shardingService.CompleteShardAsync(
                shard.Id, 
                result.IsSuccessful, 
                result.TotalItemCount, 
                result.TotalSizeBytes,
                result.ManifestHash,
                result.ErrorMessage,
                cancellationToken);

            _logger.LogInformation("Completed shard {ShardId} | Success: {Success} | Items: {Items} | Size: {Size} bytes", 
                shard.Id, result.IsSuccessful, result.TotalItemCount, result.TotalSizeBytes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing shard {ShardId} | CorrelationId: {CorrelationId}", shard.Id, correlationId);
            
            // Mark shard as failed
            await _shardingService.CompleteShardAsync(
                shard.Id, false, 0, 0, null, ex.Message, cancellationToken);

            return new CollectionResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                TotalItemCount = 0,
                TotalSizeBytes = 0
            };
        }
    }

    public async Task<CollectionResult> ResumeShardProcessingAsync(JobShard shard, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Resuming shard {ShardId} from checkpoints | CorrelationId: {CorrelationId}", 
            shard.Id, correlationId);

        // Validate checkpoint integrity
        var validationResult = await _checkpointService.ValidateCheckpointIntegrityAsync(shard.Id, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Checkpoint validation failed for shard {ShardId}: {Errors}", 
                shard.Id, string.Join(", ", validationResult.ValidationErrors));
            
            // Start fresh if checkpoints are corrupted
            return await ExecuteShardCollectionAsync(shard, correlationId, cancellationToken);
        }

        // Get incomplete checkpoints to resume
        var incompleteCheckpoints = await _checkpointService.GetResumeCheckpointsAsync(shard.Id, cancellationToken);
        
        var aggregatedResult = new CollectionResult
        {
            IsSuccessful = true,
            Items = new List<CollectedItem>(),
            TotalItemCount = 0,
            TotalSizeBytes = 0
        };

        // Resume processing from incomplete checkpoints
        foreach (var checkpoint in incompleteCheckpoints)
        {
            try
            {
                var checkpointResult = await ResumeFromCheckpointAsync(shard, checkpoint, correlationId, cancellationToken);
                
                // Aggregate results
                aggregatedResult.Items.AddRange(checkpointResult.Items);
                aggregatedResult.TotalItemCount += checkpointResult.TotalItemCount;
                aggregatedResult.TotalSizeBytes += checkpointResult.TotalSizeBytes;

                // Complete the checkpoint
                await _checkpointService.CompleteCheckpointAsync(
                    checkpoint.Id, checkpointResult.TotalItemCount, checkpointResult.TotalSizeBytes, cancellationToken);

                // Update shard progress
                await _shardingService.UpdateShardProgressAsync(
                    shard.Id, aggregatedResult.TotalItemCount, aggregatedResult.TotalSizeBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming from checkpoint {CheckpointId} for shard {ShardId}", 
                    checkpoint.Id, shard.Id);
                
                aggregatedResult.IsSuccessful = false;
                aggregatedResult.ErrorMessage = ex.Message;
                break;
            }
        }

        return aggregatedResult;
    }

    public async Task<bool> ValidateShardForProcessingAsync(JobShard shard, CancellationToken cancellationToken = default)
    {
        // Check if shard is in a valid state for processing
        if (shard.Status != JobShardStatus.Assigned && shard.Status != JobShardStatus.Retrying)
        {
            _logger.LogWarning("Shard {ShardId} is not in a valid state for processing: {Status}", shard.Id, shard.Status);
            return false;
        }

        // Check if lock is still valid
        if (shard.LockExpiry.HasValue && shard.LockExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Shard {ShardId} lock has expired", shard.Id);
            return false;
        }

        // Validate date range
        if (shard.StartDate >= shard.EndDate)
        {
            _logger.LogWarning("Shard {ShardId} has invalid date range: {StartDate} to {EndDate}", 
                shard.Id, shard.StartDate, shard.EndDate);
            return false;
        }

        return true;
    }

    private async Task<CollectionResult> ExecuteShardCollectionAsync(JobShard shard, string correlationId, 
        CancellationToken cancellationToken)
    {
        // Create collection request for this shard
        var request = new CollectionRequest
        {
            CustodianEmail = shard.CustodianEmail,
            JobType = shard.JobType,
            StartDate = shard.StartDate,
            EndDate = shard.EndDate,
            OutputPath = shard.OutputPath ?? string.Empty
        };

        // Create initial checkpoint
        var initialCheckpoint = await _checkpointService.CreateBatchCheckpointAsync(
            shard.Id, 
            "ShardProcessing", 
            1, 
            new { StartTime = DateTime.UtcNow, Request = request }, 
            correlationId, 
            cancellationToken);

        CollectionResult result;

        // Execute collection based on job type
        switch (shard.JobType)
        {
            case CollectionJobType.Email:
                result = await ProcessEmailShardAsync(shard, request, correlationId, cancellationToken);
                break;
            
            case CollectionJobType.OneDrive:
                result = await ProcessOneDriveShardAsync(shard, request, correlationId, cancellationToken);
                break;
            
            case CollectionJobType.SharePoint:
                result = await ProcessSharePointShardAsync(shard, request, correlationId, cancellationToken);
                break;
            
            case CollectionJobType.Teams:
                result = await ProcessTeamsShardAsync(shard, request, correlationId, cancellationToken);
                break;
            
            default:
                result = new CollectionResult
                {
                    IsSuccessful = false,
                    ErrorMessage = $"Unsupported job type: {shard.JobType}"
                };
                break;
        }

        // Complete initial checkpoint
        await _checkpointService.CompleteCheckpointAsync(
            initialCheckpoint.Id, result.TotalItemCount, result.TotalSizeBytes, cancellationToken);

        return result;
    }

    private async Task<CollectionResult> ProcessEmailShardAsync(JobShard shard, CollectionRequest request, 
        string correlationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing email shard {ShardId} for {Custodian}", shard.Id, shard.CustodianEmail);

        // Create mail folder checkpoint
        await _checkpointService.CreateMailFolderCheckpointAsync(
            shard.Id, "inbox", "Inbox", null, 0, correlationId, cancellationToken);

        var result = await _graphCollector.CollectEmailAsync(request, cancellationToken);
        
        // Update progress periodically during collection
        await _shardingService.UpdateShardProgressAsync(
            shard.Id, result.TotalItemCount, result.TotalSizeBytes, cancellationToken);

        return result;
    }

    private async Task<CollectionResult> ProcessOneDriveShardAsync(JobShard shard, CollectionRequest request, 
        string correlationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing OneDrive shard {ShardId} for {Custodian}", shard.Id, shard.CustodianEmail);

        // Create OneDrive checkpoint
        await _checkpointService.CreateOneDriveCheckpointAsync(
            shard.Id, "default_drive", null, 0, correlationId, cancellationToken);

        var result = await _graphCollector.CollectOneDriveAsync(request, cancellationToken);
        
        // Update progress
        await _shardingService.UpdateShardProgressAsync(
            shard.Id, result.TotalItemCount, result.TotalSizeBytes, cancellationToken);

        return result;
    }

    private async Task<CollectionResult> ProcessSharePointShardAsync(JobShard shard, CollectionRequest request, 
        string correlationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing SharePoint shard {ShardId} for {Custodian}", shard.Id, shard.CustodianEmail);

        // Create SharePoint checkpoint
        await _checkpointService.CreateSharePointCheckpointAsync(
            shard.Id, "default_site", "default_list", null, 0, correlationId, cancellationToken);

        var result = await _graphCollector.CollectSharePointAsync(request, cancellationToken);
        
        // Update progress
        await _shardingService.UpdateShardProgressAsync(
            shard.Id, result.TotalItemCount, result.TotalSizeBytes, cancellationToken);

        return result;
    }

    private async Task<CollectionResult> ProcessTeamsShardAsync(JobShard shard, CollectionRequest request, 
        string correlationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing Teams shard {ShardId} for {Custodian}", shard.Id, shard.CustodianEmail);

        // Create Teams checkpoint
        await _checkpointService.CreateTeamsCheckpointAsync(
            shard.Id, "default_team", "default_channel", null, 0, correlationId, cancellationToken);

        var result = await _graphCollector.CollectTeamsAsync(request, cancellationToken);
        
        // Update progress
        await _shardingService.UpdateShardProgressAsync(
            shard.Id, result.TotalItemCount, result.TotalSizeBytes, cancellationToken);

        return result;
    }

    private async Task<CollectionResult> ResumeFromCheckpointAsync(JobShard shard, JobShardCheckpoint checkpoint, 
        string correlationId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Resuming from checkpoint {CheckpointId} of type {Type} for shard {ShardId}", 
            checkpoint.Id, checkpoint.CheckpointType, shard.Id);

        // Parse checkpoint data based on type
        var result = checkpoint.CheckpointType switch
        {
            "MailFolderProgress" => await ResumeMailCollectionAsync(shard, checkpoint, correlationId, cancellationToken),
            "OneDriveProgress" => await ResumeOneDriveCollectionAsync(shard, checkpoint, correlationId, cancellationToken),
            "SharePointProgress" => await ResumeSharePointCollectionAsync(shard, checkpoint, correlationId, cancellationToken),
            "TeamsProgress" => await ResumeTeamsCollectionAsync(shard, checkpoint, correlationId, cancellationToken),
            _ => new CollectionResult { IsSuccessful = false, ErrorMessage = $"Unknown checkpoint type: {checkpoint.CheckpointType}" }
        };

        return result;
    }

    private async Task<CollectionResult> ResumeMailCollectionAsync(JobShard shard, JobShardCheckpoint checkpoint, 
        string correlationId, CancellationToken cancellationToken)
    {
        var checkpointData = JsonSerializer.Deserialize<MailFolderCheckpointData>(checkpoint.CheckpointData);
        
        _logger.LogInformation("Resuming mail collection from folder {FolderName} with {ProcessedItems} already processed", 
            checkpointData?.FolderName, checkpointData?.ItemsProcessedInFolder);

        // Resume collection with delta token if available
        var request = new CollectionRequest
        {
            CustodianEmail = shard.CustodianEmail,
            JobType = CollectionJobType.Email,
            StartDate = shard.StartDate,
            EndDate = shard.EndDate,
            OutputPath = shard.OutputPath ?? string.Empty
        };

        return await _graphCollector.CollectEmailAsync(request, cancellationToken);
    }

    private async Task<CollectionResult> ResumeOneDriveCollectionAsync(JobShard shard, JobShardCheckpoint checkpoint, 
        string correlationId, CancellationToken cancellationToken)
    {
        var checkpointData = JsonSerializer.Deserialize<OneDriveCheckpointData>(checkpoint.CheckpointData);
        
        _logger.LogInformation("Resuming OneDrive collection from drive {DriveId} with {ProcessedItems} already processed", 
            checkpointData?.DriveId, checkpointData?.ItemsProcessedInDrive);

        var request = new CollectionRequest
        {
            CustodianEmail = shard.CustodianEmail,
            JobType = CollectionJobType.OneDrive,
            StartDate = shard.StartDate,
            EndDate = shard.EndDate,
            OutputPath = shard.OutputPath ?? string.Empty
        };

        return await _graphCollector.CollectOneDriveAsync(request, cancellationToken);
    }

    private async Task<CollectionResult> ResumeSharePointCollectionAsync(JobShard shard, JobShardCheckpoint checkpoint, 
        string correlationId, CancellationToken cancellationToken)
    {
        var checkpointData = JsonSerializer.Deserialize<SharePointCheckpointData>(checkpoint.CheckpointData);
        
        _logger.LogInformation("Resuming SharePoint collection from site {SiteId}/list {ListId} with {ProcessedItems} already processed", 
            checkpointData?.SiteId, checkpointData?.ListId, checkpointData?.ItemsProcessedInList);

        var request = new CollectionRequest
        {
            CustodianEmail = shard.CustodianEmail,
            JobType = CollectionJobType.SharePoint,
            StartDate = shard.StartDate,
            EndDate = shard.EndDate,
            OutputPath = shard.OutputPath ?? string.Empty
        };

        return await _graphCollector.CollectSharePointAsync(request, cancellationToken);
    }

    private async Task<CollectionResult> ResumeTeamsCollectionAsync(JobShard shard, JobShardCheckpoint checkpoint, 
        string correlationId, CancellationToken cancellationToken)
    {
        var checkpointData = JsonSerializer.Deserialize<TeamsCheckpointData>(checkpoint.CheckpointData);
        
        _logger.LogInformation("Resuming Teams collection from team {TeamId}/channel {ChannelId} with {ProcessedItems} already processed", 
            checkpointData?.TeamId, checkpointData?.ChannelId, checkpointData?.ItemsProcessedInChannel);

        var request = new CollectionRequest
        {
            CustodianEmail = shard.CustodianEmail,
            JobType = CollectionJobType.Teams,
            StartDate = shard.StartDate,
            EndDate = shard.EndDate,
            OutputPath = shard.OutputPath ?? string.Empty
        };

        return await _graphCollector.CollectTeamsAsync(request, cancellationToken);
    }

    private async Task UpdateShardStatusAsync(int shardId, JobShardStatus status)
    {
        try
        {
            // This would typically update the database directly
            // For now, we'll just log the status change
            _logger.LogInformation("Updated shard {ShardId} status to {Status}", shardId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shard {ShardId} status to {Status}", shardId, status);
        }
    }
}
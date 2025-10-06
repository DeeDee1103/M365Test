using HybridGraphCollectorWorker.Models;
using HybridGraphCollectorWorker.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HybridGraphCollectorWorker.Workers;

/// <summary>
/// Background worker that watches for completed GDC runs and triggers binary fetch operations
/// </summary>
public class GdcFetchWorker : BackgroundService
{
    private readonly ILogger<GdcFetchWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly GdcBinaryFetchOptions _options;
    private readonly IEDiscoveryApiClient _apiClient;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly Timer? _pollTimer;

    public GdcFetchWorker(
        ILogger<GdcFetchWorker> logger,
        IServiceProvider serviceProvider,
        IOptions<GdcBinaryFetchOptions> options,
        IEDiscoveryApiClient apiClient)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _apiClient = apiClient;

        // Set up file system watcher for NAS input
        if (_options.Input.Kind.Equals("nas", StringComparison.OrdinalIgnoreCase))
        {
            var watchPath = _options.Input.Nas.Root;
            if (Directory.Exists(watchPath))
            {
                _fileWatcher = new FileSystemWatcher(watchPath, "*.*")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
                };
                _fileWatcher.Created += OnFileCreated;
                _fileWatcher.EnableRaisingEvents = true;
            }
        }

        // Set up polling timer for blob input or as fallback
        _pollTimer = new Timer(PollForNewRuns, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GDC Fetch Worker started. Input kind: {InputKind}", _options.Input.Kind);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Main worker loop - the actual work is triggered by file watcher or timer
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GDC Fetch Worker is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GDC Fetch Worker encountered an unexpected error");
        }
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Look for success markers or JSON files that indicate completed GDC run
            if (IsGdcCompletionMarker(e.FullPath))
            {
                _logger.LogInformation("Detected GDC completion marker: {FilePath}", e.FullPath);
                await ProcessGdcCompletionAsync(e.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file creation event for: {FilePath}", e.FullPath);
        }
    }

    private async void PollForNewRuns(object? state)
    {
        try
        {
            if (_options.Input.Kind.Equals("blob", StringComparison.OrdinalIgnoreCase))
            {
                await PollBlobStorageAsync();
            }
            else
            {
                await PollNasStorageAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during polling for new GDC runs");
        }
    }

    private async Task PollBlobStorageAsync()
    {
        // TODO: Implement blob storage polling
        // This would involve checking for new blobs in the configured container/prefix
        // and tracking processed runs to avoid duplicates
        _logger.LogDebug("Polling blob storage for new GDC runs (not yet implemented)");
        await Task.Delay(100);
    }

    private async Task PollNasStorageAsync()
    {
        var watchPath = _options.Input.Nas.Root;
        if (!Directory.Exists(watchPath))
        {
            _logger.LogWarning("GDC watch path does not exist: {WatchPath}", watchPath);
            return;
        }

        // Look for completion markers or recent JSON files
        var recentFiles = Directory.GetFiles(watchPath, "*.*", SearchOption.AllDirectories)
            .Where(f => File.GetCreationTime(f) > DateTime.Now.AddMinutes(-30))
            .Where(IsGdcCompletionMarker);

        foreach (var file in recentFiles)
        {
            try
            {
                await ProcessGdcCompletionAsync(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GDC completion file: {FilePath}", file);
            }
        }
    }

    private static bool IsGdcCompletionMarker(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        // Look for ADF success markers
        if (fileName.Equals("_SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Look for JSON dataset files
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
            (fileName.Contains("files") || fileName.Contains("dataset")))
        {
            return true;
        }

        // Look for completion marker files
        if (fileName.EndsWith(".complete", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private async Task ProcessGdcCompletionAsync(string completionFilePath)
    {
        _logger.LogInformation("Processing GDC completion: {FilePath}", completionFilePath);

        try
        {
            // Determine the dataset files to process
            var datasetFiles = await DiscoverDatasetFilesAsync(completionFilePath);
            
            if (!datasetFiles.Any())
            {
                _logger.LogWarning("No dataset files found for completion marker: {FilePath}", completionFilePath);
                return;
            }

            // Process each dataset file
            foreach (var datasetFile in datasetFiles)
            {
                await ProcessDatasetFileAsync(datasetFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GDC completion: {FilePath}", completionFilePath);
        }
    }

    private async Task<List<string>> DiscoverDatasetFilesAsync(string completionFilePath)
    {
        await Task.Delay(1); // Minimal async operation
        
        var datasetFiles = new List<string>();
        var directory = Path.GetDirectoryName(completionFilePath);

        if (string.IsNullOrEmpty(directory))
        {
            return datasetFiles;
        }

        // If the completion file itself is a JSON dataset, use it
        if (completionFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            datasetFiles.Add(completionFilePath);
            return datasetFiles;
        }

        // Look for JSON files in the same directory or subdirectories
        var jsonFiles = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.Equals(completionFilePath, StringComparison.OrdinalIgnoreCase));

        datasetFiles.AddRange(jsonFiles);

        _logger.LogInformation("Discovered {DatasetFileCount} dataset files for completion marker: {CompletionFile}", 
            datasetFiles.Count, completionFilePath);

        return datasetFiles;
    }

    private async Task ProcessDatasetFileAsync(string datasetFilePath)
    {
        _logger.LogInformation("Processing GDC dataset file: {DatasetFilePath}", datasetFilePath);

        try
        {
            // Extract metadata from file path or content
            var metadata = await ExtractDatasetMetadataAsync(datasetFilePath);

            // Create job in EDiscovery API
            var jobRequest = new
            {
                custodianEmail = metadata.Custodian,
                matterName = metadata.MatterName,
                jobType = "GdcBinaryFetch",
                route = "GdcBinaryFetch",
                metadata = new Dictionary<string, object>
                {
                    ["datasetFilePath"] = datasetFilePath,
                    ["gdcRunId"] = metadata.GdcRunId,
                    ["estimatedFileCount"] = metadata.EstimatedFileCount
                }
            };

            // Start job via API
            var jobResponse = await _apiClient.CreateJobAsync(jobRequest);
            var jobId = ExtractJobIdFromResponse(jobResponse);

            if (jobId > 0)
            {
                _logger.LogInformation("Created GDC binary fetch job {JobId} for dataset: {DatasetFilePath}", 
                    jobId, datasetFilePath);

                // Process the dataset using GdcBinaryFetcher
                var fetchResult = await ExecuteGdcBinaryFetchAsync(jobId, datasetFilePath, metadata);

                // Complete the job
                await _apiClient.CompleteJobAsync(jobId, fetchResult.Success, fetchResult.TotalBytesDownloaded, fetchResult.SuccessfulDownloads);
                _logger.LogInformation("Completed GDC binary fetch job {JobId}", jobId);
            }
            else
            {
                _logger.LogError("Failed to create job for dataset: {DatasetFilePath}", datasetFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dataset file: {DatasetFilePath}", datasetFilePath);
        }
    }

    private async Task<GdcDatasetMetadata> ExtractDatasetMetadataAsync(string datasetFilePath)
    {
        var metadata = new GdcDatasetMetadata
        {
            DatasetFilePath = datasetFilePath,
            GdcRunId = Guid.NewGuid().ToString(), // Generate if not available
            MatterName = "DefaultMatter", // Extract from path if possible
            Custodian = "unknown" // Extract from path or content if possible
        };

        try
        {
            // Try to extract custodian from file path
            var pathParts = datasetFilePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            
            // Look for email-like patterns in path
            foreach (var part in pathParts.Reverse())
            {
                if (part.Contains('@'))
                {
                    metadata.Custodian = part;
                    break;
                }
            }

            // Try to extract matter name from path
            if (pathParts.Length > 2)
            {
                metadata.MatterName = pathParts[^3]; // Third from end
            }

            // Quick scan of file to estimate record count
            if (File.Exists(datasetFilePath))
            {
                var lines = await File.ReadAllLinesAsync(datasetFilePath);
                metadata.EstimatedFileCount = lines.Count(line => !string.IsNullOrWhiteSpace(line));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting metadata from dataset file: {DatasetFilePath}", datasetFilePath);
        }

        return metadata;
    }

    private async Task<HybridGraphCollectorWorker.Services.GdcBinaryFetchResult> ExecuteGdcBinaryFetchAsync(int jobId, string datasetFilePath, GdcDatasetMetadata metadata)
    {
        using var scope = _serviceProvider.CreateScope();
        var binaryFetcher = scope.ServiceProvider.GetRequiredService<GdcBinaryFetcher>();

        var result = await binaryFetcher.FetchBinariesAsync(
            datasetFilePath,
            metadata.Custodian,
            jobId,
            metadata.MatterName,
            _options);

        // Update job with results
        var updateRequest = new
        {
            status = result.Success ? "Completed" : "Failed",
            itemsCollected = result.SuccessfulDownloads,
            totalBytes = result.TotalBytesDownloaded,
            errorMessage = result.ErrorMessage,
            metadata = new Dictionary<string, object>
            {
                ["totalRecords"] = result.TotalRecords,
                ["successfulDownloads"] = result.SuccessfulDownloads,
                ["failedDownloads"] = result.FailedDownloads,
                ["skippedRecords"] = result.SkippedRecords,
                ["processingDuration"] = result.Duration.ToString()
            }
        };

        await _apiClient.UpdateJobAsync(jobId, updateRequest);

        _logger.LogInformation("Updated job {JobId} with GDC binary fetch results", jobId);
        
        return result;
    }

    private static int ExtractJobIdFromResponse(object response)
    {
        // This would depend on the actual API response format
        // For now, return a placeholder
        return new Random().Next(1000, 9999);
    }

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        _pollTimer?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Metadata extracted from GDC dataset
/// </summary>
public class GdcDatasetMetadata
{
    public string DatasetFilePath { get; set; } = string.Empty;
    public string GdcRunId { get; set; } = string.Empty;
    public string MatterName { get; set; } = string.Empty;
    public string Custodian { get; set; } = string.Empty;
    public int EstimatedFileCount { get; set; }
}
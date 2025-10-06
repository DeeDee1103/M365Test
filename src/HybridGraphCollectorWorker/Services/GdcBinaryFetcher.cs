using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using HybridGraphCollectorWorker.Models;
using HybridGraphCollectorWorker.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace HybridGraphCollectorWorker.Services;

/// <summary>
/// Service for fetching binary content from Microsoft Graph based on GDC dataset JSON
/// </summary>
public class GdcBinaryFetcher
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GdcBinaryFetcher> _logger;
    private readonly RetryPolicy _retryPolicy;
    private readonly string _outputBasePath;
    private readonly SemaphoreSlim _semaphore;

    public GdcBinaryFetcher(
        IConfiguration configuration,
        ILogger<GdcBinaryFetcher> logger)
    {
        _logger = logger;
        _outputBasePath = configuration["OutputPath"] ?? "./output";
        _retryPolicy = new RetryPolicy(logger);

        // Configure Graph Client
        var tenantId = configuration["AzureAd:TenantId"];
        var clientId = configuration["AzureAd:ClientId"];
        var clientSecret = configuration["AzureAd:ClientSecret"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential);

        // Initialize semaphore for parallelism control
        var parallelism = configuration.GetValue<int>("Gdc:Parallelism", 8);
        _semaphore = new SemaphoreSlim(parallelism, parallelism);
    }

    /// <summary>
    /// Process GDC dataset and fetch binary content for each file
    /// </summary>
    public async Task<GdcBinaryFetchResult> FetchBinariesAsync(
        string gdcDatasetPath,
        string custodian,
        int jobId,
        string matterName,
        GdcBinaryFetchOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting GDC binary fetch for custodian: {Custodian}, JobId: {JobId}, Dataset: {DatasetPath}",
            custodian, jobId, gdcDatasetPath);

        var result = new GdcBinaryFetchResult
        {
            JobId = jobId,
            Custodian = custodian,
            MatterName = matterName,
            StartTime = DateTime.UtcNow
        };

        var manifestEntries = new List<GdcManifestEntry>();

        try
        {
            // Read and parse GDC dataset
            var fileRecords = await ReadGdcDatasetAsync(gdcDatasetPath, cancellationToken);
            _logger.LogInformation("Loaded {RecordCount} records from GDC dataset", fileRecords.Count);

            // Filter records
            var filteredRecords = FilterRecords(fileRecords, options.Filters);
            _logger.LogInformation("Processing {FilteredCount} records after applying filters", filteredRecords.Count);

            result.TotalRecords = filteredRecords.Count;

            // Process records in parallel with controlled concurrency
            var tasks = filteredRecords.Select(async (record, index) =>
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ProcessFileRecordAsync(record, custodian, matterName, options, index, cancellationToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            });

            var processResults = await Task.WhenAll(tasks);

            // Aggregate results
            foreach (var processResult in processResults)
            {
                if (processResult.Success)
                {
                    result.SuccessfulDownloads++;
                    result.TotalBytesDownloaded += processResult.BytesDownloaded;

                    manifestEntries.Add(new GdcManifestEntry
                    {
                        Custodian = custodian,
                        Kind = "File",
                        DriveId = processResult.Record.ResolvedDriveId,
                        ItemId = processResult.Record.ItemId,
                        Path = processResult.Record.Path,
                        Size = processResult.BytesDownloaded,
                        Sha256 = processResult.Sha256Hash ?? string.Empty,
                        StorageUri = processResult.OutputPath ?? string.Empty,
                        CollectedUtc = DateTime.UtcNow,
                        OriginalPath = processResult.Record.Path,
                        MimeType = processResult.Record.File?.MimeType,
                        LastModifiedDateTime = processResult.Record.LastModifiedDateTime
                    });
                }
                else if (!string.IsNullOrEmpty(processResult.SkipReason))
                {
                    result.SkippedRecords++;
                    _logger.LogWarning("Skipped record {RecordKey}: {SkipReason}", 
                        processResult.RecordKey, processResult.SkipReason);
                }
                else
                {
                    result.FailedDownloads++;
                    _logger.LogError("Failed to process record {RecordKey}: {ErrorMessage}", 
                        processResult.RecordKey, processResult.ErrorMessage);

                    // Check if we've hit max errors
                    if (result.FailedDownloads >= options.MaxErrors)
                    {
                        _logger.LogError("Maximum error count ({MaxErrors}) reached, stopping job", options.MaxErrors);
                        break;
                    }
                }
            }

            // Write manifests
            if (manifestEntries.Count > 0)
            {
                await WriteManifestsAsync(manifestEntries, custodian, matterName, jobId, cancellationToken);
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = result.FailedDownloads < options.MaxErrors;

            _logger.LogInformation(
                "GDC binary fetch completed. Success: {Success}, Downloaded: {Downloaded}, Failed: {Failed}, Skipped: {Skipped}, Total Bytes: {TotalBytes}",
                result.Success, result.SuccessfulDownloads, result.FailedDownloads, result.SkippedRecords, result.TotalBytesDownloaded);

            // Emit structured log for dashboards
            _logger.LogInformation("PERFORMANCE: GDC_BINARY_FETCH_COMPLETED JobId={JobId} Custodian={Custodian} " +
                "Downloaded={Downloaded} Failed={Failed} Skipped={Skipped} TotalBytes={TotalBytes} DurationMs={DurationMs}",
                jobId, custodian, result.SuccessfulDownloads, result.FailedDownloads, result.SkippedRecords,
                result.TotalBytesDownloaded, (result.EndTime - result.StartTime).TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Fatal error during GDC binary fetch for JobId: {JobId}", jobId);
            return result;
        }
    }

    private async Task<List<GdcFileRecord>> ReadGdcDatasetAsync(string datasetPath, CancellationToken cancellationToken)
    {
        var records = new List<GdcFileRecord>();

        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException($"GDC dataset file not found: {datasetPath}");
        }

        var content = await File.ReadAllTextAsync(datasetPath, cancellationToken);

        // Handle both line-delimited JSON and array JSON
        if (content.TrimStart().StartsWith("["))
        {
            // Array JSON format
            var arrayRecords = JsonSerializer.Deserialize<GdcFileRecord[]>(content);
            if (arrayRecords != null)
            {
                records.AddRange(arrayRecords);
            }
        }
        else
        {
            // Line-delimited JSON format
            using var reader = new StringReader(content);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var record = JsonSerializer.Deserialize<GdcFileRecord>(line);
                    if (record != null)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Failed to parse JSON line: {Line}. Error: {Error}", line, ex.Message);
                }
            }
        }

        return records;
    }

    private List<GdcFileRecord> FilterRecords(List<GdcFileRecord> records, GdcFiltersOptions filters)
    {
        return records.Where(record =>
        {
            // Only process files, not folders
            if (!record.IsFile) return false;

            // Check file extensions
            if (filters.FileExtensions.Length > 0 && !filters.FileExtensions.Contains("*"))
            {
                var fileName = record.ResolvedFileName;
                if (string.IsNullOrEmpty(fileName)) return false;

                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                var allowedExtensions = filters.FileExtensions.Select(ext => ext.ToLowerInvariant()).ToArray();
                if (!allowedExtensions.Contains(extension) && !allowedExtensions.Contains("*"))
                {
                    return false;
                }
            }

            // Check modified date
            if (filters.ModifiedAfter.HasValue && record.LastModifiedDateTime.HasValue)
            {
                if (record.LastModifiedDateTime.Value < filters.ModifiedAfter.Value)
                {
                    return false;
                }
            }

            return true;
        }).ToList();
    }

    private async Task<GdcFileProcessResult> ProcessFileRecordAsync(
        GdcFileRecord record,
        string custodian,
        string matterName,
        GdcBinaryFetchOptions options,
        int index,
        CancellationToken cancellationToken)
    {
        var recordKey = $"{record.ResolvedDriveId}/{record.ItemId}";
        var startTime = DateTime.UtcNow;

        var result = new GdcFileProcessResult
        {
            RecordKey = recordKey,
            Record = record
        };

        try
        {
            // Validate required identifiers
            if (string.IsNullOrEmpty(record.ResolvedDriveId) || string.IsNullOrEmpty(record.ItemId))
            {
                if (!string.IsNullOrEmpty(record.WebUrl))
                {
                    // TODO: Could attempt to resolve from webUrl, but for now skip
                    result.SkipReason = "SkippedNoId - webUrl resolution not implemented";
                    _logger.LogInformation("DATA_COLLECTION: SkippedNoId RecordKey={RecordKey} Reason=NoUsableId", recordKey);
                    return result;
                }

                result.SkipReason = "SkippedNoId - missing driveId or itemId";
                _logger.LogInformation("DATA_COLLECTION: SkippedNoId RecordKey={RecordKey} Reason=MissingIds", recordKey);
                return result;
            }

            // Generate output path
            var fileName = record.ResolvedFileName ?? "unknown_file";
            var sanitizedFileName = SanitizeFileName(fileName);
            var outputDir = Path.Combine(_outputBasePath, "matter", matterName, "GDC", custodian);
            var outputPath = Path.Combine(outputDir, $"{index:D6}_{sanitizedFileName}");

            // Check if file already exists with same hash (idempotency)
            if (File.Exists(outputPath) && !options.DryRun)
            {
                var existingSha256 = await ComputeFileSha256Async(outputPath, cancellationToken);
                // For now, we'll re-download; could add logic to compare with GDC hash if available
            }

            Directory.CreateDirectory(outputDir);

            if (options.DryRun)
            {
                _logger.LogInformation("DATA_COLLECTION: DryRun RecordKey={RecordKey} OutputPath={OutputPath}", 
                    recordKey, outputPath);
                result.Success = true;
                result.OutputPath = outputPath;
                result.Sha256Hash = "dry-run-mode";
                return result;
            }

            // Download file content from Graph API
            var downloadResult = await DownloadFileContentAsync(record, outputPath, cancellationToken);

            result.Success = downloadResult.Success;
            result.BytesDownloaded = downloadResult.BytesDownloaded;
            result.Sha256Hash = downloadResult.Sha256Hash;
            result.OutputPath = outputPath;
            result.ErrorMessage = downloadResult.ErrorMessage;

            if (result.Success)
            {
                _logger.LogInformation("DATA_COLLECTION: BinaryFetched RecordKey={RecordKey} Size={Size} SHA256={SHA256}", 
                    recordKey, result.BytesDownloaded, result.Sha256Hash);
            }
            else
            {
                _logger.LogError("DATA_COLLECTION: Failed RecordKey={RecordKey} Error={Error}", 
                    recordKey, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error processing record {RecordKey}", recordKey);
            return result;
        }
        finally
        {
            result.ProcessingTime = DateTime.UtcNow - startTime;
        }
    }

    private async Task<FileDownloadResult> DownloadFileContentAsync(
        GdcFileRecord record,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new FileDownloadResult();

        try
        {
            // Use retry policy for Graph API calls
            var stream = await _retryPolicy.ExecuteAsync(async () =>
            {
                _logger.LogDebug("GRAPH_API: Downloading content for DriveId={DriveId} ItemId={ItemId}", 
                    record.ResolvedDriveId, record.ItemId);

                return await _graphClient.Drives[record.ResolvedDriveId]
                    .Items[record.ItemId]
                    .Content
                    .GetAsync(cancellationToken: cancellationToken);
            });

            if (stream == null)
            {
                result.ErrorMessage = "No content stream returned from Graph API";
                return result;
            }

            // Download and compute hash
            using (stream)
            using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (var sha256 = SHA256.Create())
            {
                var buffer = new byte[81920]; // 80KB buffer
                int bytesRead;
                long totalBytes = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                    totalBytes += bytesRead;
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                result.Sha256Hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
                result.BytesDownloaded = totalBytes;
                result.Success = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error downloading file content for DriveId={DriveId} ItemId={ItemId}", 
                record.ResolvedDriveId, record.ItemId);

            // Clean up partial file
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            return result;
        }
    }

    private async Task WriteManifestsAsync(
        List<GdcManifestEntry> entries,
        string custodian,
        string matterName,
        int jobId,
        CancellationToken cancellationToken)
    {
        var manifestDir = Path.Combine(_outputBasePath, "logs", matterName, jobId.ToString());
        Directory.CreateDirectory(manifestDir);

        var csvPath = Path.Combine(manifestDir, "manifest.csv");
        var jsonPath = Path.Combine(manifestDir, "manifest.json");

        // Write CSV manifest
        await using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
        {
            // Write header
            await writer.WriteLineAsync("Custodian,Kind,DriveId,ItemId,Path,Size,SHA256,StorageUri,CollectedUtc");

            // Write entries
            foreach (var entry in entries)
            {
                var line = $"{EscapeCsv(entry.Custodian)},{EscapeCsv(entry.Kind)},{EscapeCsv(entry.DriveId)}," +
                          $"{EscapeCsv(entry.ItemId)},{EscapeCsv(entry.Path)},{entry.Size}," +
                          $"{entry.Sha256},{EscapeCsv(entry.StorageUri)},{entry.CollectedUtc:yyyy-MM-ddTHH:mm:ss.fffZ}";
                await writer.WriteLineAsync(line);
            }
        }

        // Write JSON manifest
        var jsonContent = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(jsonPath, jsonContent, cancellationToken);

        // Write manifest hash file
        var manifestContent = await File.ReadAllTextAsync(csvPath, cancellationToken);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(manifestContent));
        var manifestHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        
        var hashPath = Path.Combine(manifestDir, "manifest.sha256");
        await File.WriteAllTextAsync(hashPath, manifestHash, cancellationToken);

        _logger.LogInformation("CHAIN_OF_CUSTODY: ManifestCreated JobId={JobId} Custodian={Custodian} " +
            "Entries={EntryCount} CSVPath={CSVPath} JSONPath={JSONPath} HashPath={HashPath}",
            jobId, custodian, entries.Count, csvPath, jsonPath, hashPath);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder();

        foreach (var c in fileName)
        {
            if (invalidChars.Contains(c))
            {
                sanitized.Append('_');
            }
            else
            {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static async Task<string> ComputeFileSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

/// <summary>
/// Result of file download operation
/// </summary>
public class FileDownloadResult
{
    public bool Success { get; set; }
    public long BytesDownloaded { get; set; }
    public string? Sha256Hash { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Overall result of GDC binary fetch operation
/// </summary>
public class GdcBinaryFetchResult
{
    public int JobId { get; set; }
    public string Custodian { get; set; } = string.Empty;
    public string MatterName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalRecords { get; set; }
    public int SuccessfulDownloads { get; set; }
    public int FailedDownloads { get; set; }
    public int SkippedRecords { get; set; }
    public long TotalBytesDownloaded { get; set; }

    public TimeSpan Duration => EndTime - StartTime;
}
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Globalization;
using HybridGraphCollectorWorker.Models;
using EDiscovery.Shared.Services;

namespace HybridGraphCollectorWorker.Services
{
    /// <summary>
    /// Service for reconciling source manifests against collected manifests
    /// </summary>
    public class Reconciler
    {
        private readonly ILogger<Reconciler> _logger;
        private readonly ReconcileOptions _options;
        private readonly ComplianceLogger _complianceLogger;

        public Reconciler(
            ILogger<Reconciler> logger,
            IOptions<ReconcileOptions> options,
            ComplianceLogger complianceLogger)
        {
            _logger = logger;
            _options = options.Value;
            _complianceLogger = complianceLogger;
        }

        /// <summary>
        /// Perform reconciliation between source and collected manifests
        /// </summary>
        public async Task<ReconciliationResult> ReconcileAsync(
            string custodian,
            string jobId,
            string sourceManifestPath,
            string collectedManifestPath,
            CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            
            _logger.LogInformation("Starting reconciliation for custodian {Custodian}, job {JobId} | CorrelationId: {CorrelationId}",
                custodian, jobId, correlationId);

            var result = new ReconciliationResult
            {
                JobId = jobId,
                Custodian = custodian,
                ProcessedUtc = DateTime.UtcNow,
                SizeTolerancePct = _options.SizeTolerancePct,
                ExtraTolerancePct = _options.ExtraTolerancePct,
                RequireHashMatch = _options.RequireHashMatch
            };

            try
            {
                // Load manifests
                var sourceItems = await LoadManifestAsync(sourceManifestPath, "source", cancellationToken);
                var collectedItems = await LoadManifestAsync(collectedManifestPath, "collected", cancellationToken);

                _logger.LogInformation("Loaded {SourceCount} source items and {CollectedCount} collected items",
                    sourceItems.Count, collectedItems.Count);

                // Filter and normalize
                var filteredSource = FilterAndNormalize(sourceItems, custodian);
                var filteredCollected = FilterAndNormalize(collectedItems, custodian);

                result.SourceCount = filteredSource.Count;
                result.CollectedCount = filteredCollected.Count;
                result.SourceTotalBytes = filteredSource.Sum(x => x.Size);
                result.CollectedTotalBytes = filteredCollected.Sum(x => x.Size);

                // Perform reconciliation checks
                await PerformReconciliationChecksAsync(result, filteredSource, filteredCollected, correlationId);

                // Generate report
                if (!_options.DryRun)
                {
                    result.ReportPath = await GenerateReportAsync(result, correlationId);
                }

                // Log compliance event
                await LogReconciliationResultAsync(result, correlationId);

                _logger.LogInformation("Reconciliation completed for custodian {Custodian}, job {JobId}. Result: {Result} | CorrelationId: {CorrelationId}",
                    custodian, jobId, result.OverallPassed ? "PASS" : "FAIL", correlationId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reconciliation for custodian {Custodian}, job {JobId} | CorrelationId: {CorrelationId}",
                    custodian, jobId, correlationId);
                throw;
            }
        }

        /// <summary>
        /// Load manifest from file (CSV or JSON format)
        /// </summary>
        private async Task<List<ManifestItem>> LoadManifestAsync(
            string filePath,
            string manifestType,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Manifest file not found: {FilePath}", filePath);
                return new List<ManifestItem>();
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            switch (extension)
            {
                case ".json":
                case ".jsonl":
                    return await LoadJsonManifestAsync(filePath, cancellationToken);
                case ".csv":
                    return await LoadCsvManifestAsync(filePath, cancellationToken);
                default:
                    throw new NotSupportedException($"Unsupported manifest format: {extension}");
            }
        }

        /// <summary>
        /// Load JSON manifest (array or line-delimited)
        /// </summary>
        private async Task<List<ManifestItem>> LoadJsonManifestAsync(string filePath, CancellationToken cancellationToken)
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var items = new List<ManifestItem>();

            try
            {
                // Try parsing as JSON array first
                var arrayItems = JsonSerializer.Deserialize<ManifestItem[]>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (arrayItems != null)
                {
                    items.AddRange(arrayItems);
                    return items;
                }
            }
            catch
            {
                // Fall back to line-delimited JSON
            }

            // Parse as line-delimited JSON
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var item = JsonSerializer.Deserialize<ManifestItem>(line.Trim(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to parse JSON line: {Line}. Error: {Error}", line, ex.Message);
                }
            }

            return items;
        }

        /// <summary>
        /// Load CSV manifest
        /// </summary>
        private async Task<List<ManifestItem>> LoadCsvManifestAsync(string filePath, CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            var items = new List<ManifestItem>();

            if (lines.Length == 0) return items;

            // Parse header
            var headers = lines[0].Split(',').Select(h => h.Trim(' ', '"')).ToArray();
            var columnMap = MapCsvColumns(headers);

            // Parse data rows
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length != headers.Length)
                    {
                        _logger.LogWarning("CSV line {LineNumber} has {ValueCount} values but {HeaderCount} headers", 
                            i + 1, values.Length, headers.Length);
                        continue;
                    }

                    var item = CreateItemFromCsv(values, columnMap);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to parse CSV line {LineNumber}: {Error}", i + 1, ex.Message);
                }
            }

            return items;
        }

        /// <summary>
        /// Map CSV column headers to known fields
        /// </summary>
        private Dictionary<string, int> MapCsvColumns(string[] headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].ToLowerInvariant();
                map[header] = i;

                // Handle common variations
                switch (header)
                {
                    case "drive_id":
                    case "driveid":
                        map["driveid"] = i;
                        break;
                    case "item_id":
                    case "itemid":
                    case "id":
                        map["itemid"] = i;
                        break;
                    case "file_path":
                    case "filepath":
                    case "path":
                        map["path"] = i;
                        break;
                    case "file_size":
                    case "filesize":
                    case "size":
                        map["size"] = i;
                        break;
                    case "last_modified":
                    case "lastmodified":
                    case "modified":
                        map["lastmodified"] = i;
                        break;
                    case "sha256":
                    case "hash":
                        map["sha256"] = i;
                        break;
                    case "storage_uri":
                    case "storageuri":
                        map["storageuri"] = i;
                        break;
                    case "collected_utc":
                    case "collectedutc":
                    case "collected":
                        map["collectedutc"] = i;
                        break;
                }
            }

            return map;
        }

        /// <summary>
        /// Parse CSV line with proper quote handling
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString());
            return values.ToArray();
        }

        /// <summary>
        /// Create ManifestItem from CSV values
        /// </summary>
        private ManifestItem? CreateItemFromCsv(string[] values, Dictionary<string, int> columnMap)
        {
            var item = new ManifestItem();

            if (columnMap.TryGetValue("custodian", out int custodianIdx) && custodianIdx < values.Length)
                item.Custodian = values[custodianIdx].Trim(' ', '"');

            if (columnMap.TryGetValue("driveid", out int driveIdIdx) && driveIdIdx < values.Length)
                item.DriveId = values[driveIdIdx].Trim(' ', '"');

            if (columnMap.TryGetValue("itemid", out int itemIdIdx) && itemIdIdx < values.Length)
                item.ItemId = values[itemIdIdx].Trim(' ', '"');

            if (columnMap.TryGetValue("path", out int pathIdx) && pathIdx < values.Length)
                item.Path = values[pathIdx].Trim(' ', '"');

            if (columnMap.TryGetValue("size", out int sizeIdx) && sizeIdx < values.Length)
            {
                if (long.TryParse(values[sizeIdx].Trim(' ', '"'), out long size))
                    item.Size = size;
            }

            if (columnMap.TryGetValue("lastmodified", out int modifiedIdx) && modifiedIdx < values.Length)
            {
                if (DateTime.TryParse(values[modifiedIdx].Trim(' ', '"'), out DateTime modified))
                    item.LastModified = modified;
            }

            if (columnMap.TryGetValue("sha256", out int sha256Idx) && sha256Idx < values.Length)
                item.Sha256 = values[sha256Idx].Trim(' ', '"');

            if (columnMap.TryGetValue("storageuri", out int storageIdx) && storageIdx < values.Length)
                item.StorageUri = values[storageIdx].Trim(' ', '"');

            if (columnMap.TryGetValue("collectedutc", out int collectedIdx) && collectedIdx < values.Length)
            {
                if (DateTime.TryParse(values[collectedIdx].Trim(' ', '"'), out DateTime collected))
                    item.CollectedUtc = collected;
            }

            if (columnMap.TryGetValue("kind", out int kindIdx) && kindIdx < values.Length)
                item.Kind = values[kindIdx].Trim(' ', '"');

            // Validate required fields
            if (string.IsNullOrEmpty(item.Path))
                return null;

            return item;
        }

        /// <summary>
        /// Filter and normalize items for comparison
        /// </summary>
        private List<ManifestItem> FilterAndNormalize(List<ManifestItem> items, string custodian)
        {
            var filtered = items.Where(item =>
            {
                // Filter by custodian if specified
                if (!string.IsNullOrEmpty(custodian) && 
                    !string.IsNullOrEmpty(item.Custodian) &&
                    !item.Custodian.Equals(custodian, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Filter folders if not included
                if (!_options.IncludeFolders && item.IsFolder())
                    return false;

                // Filter out system paths (RecoverableItems, versions, Recycle Bin)
                var normalizedPath = item.GetNormalizedPath();
                if (normalizedPath.Contains("/recoverableitems/") ||
                    normalizedPath.Contains("/versions/") ||
                    normalizedPath.Contains("/recyclebin/"))
                    return false;

                return true;
            }).ToList();

            // Normalize paths if enabled
            if (_options.NormalizePaths)
            {
                foreach (var item in filtered)
                {
                    item.Path = item.GetNormalizedPath();
                }
            }

            return filtered;
        }

        /// <summary>
        /// Perform reconciliation checks
        /// </summary>
        private async Task PerformReconciliationChecksAsync(
            ReconciliationResult result,
            List<ManifestItem> sourceItems,
            List<ManifestItem> collectedItems,
            string correlationId)
        {
            // Create lookup dictionaries
            var sourceDict = sourceItems.ToDictionary(x => x.GetPrimaryKey(), x => x);
            var collectedDict = collectedItems.ToDictionary(x => x.GetPrimaryKey(), x => x);

            // Find missed items (in source but not collected)
            foreach (var sourceItem in sourceItems)
            {
                var key = sourceItem.GetPrimaryKey();
                if (!collectedDict.ContainsKey(key))
                {
                    result.Missed.Add(new DiscrepancyItem
                    {
                        Key = key,
                        Type = "Missed",
                        Custodian = sourceItem.Custodian,
                        DriveId = sourceItem.DriveId,
                        ItemId = sourceItem.ItemId,
                        Path = sourceItem.Path,
                        Size = sourceItem.Size,
                        LastModified = sourceItem.LastModified,
                        SourceSha256 = sourceItem.Sha256,
                        Reason = "Item present in source manifest but not found in collected manifest"
                    });
                }
            }

            // Find extra items (collected but not in source)
            foreach (var collectedItem in collectedItems)
            {
                var key = collectedItem.GetPrimaryKey();
                if (!sourceDict.ContainsKey(key))
                {
                    result.Extras.Add(new DiscrepancyItem
                    {
                        Key = key,
                        Type = "Extra",
                        Custodian = collectedItem.Custodian,
                        DriveId = collectedItem.DriveId,
                        ItemId = collectedItem.ItemId,
                        Path = collectedItem.Path,
                        Size = collectedItem.Size,
                        LastModified = collectedItem.LastModified,
                        CollectedSha256 = collectedItem.Sha256,
                        Reason = "Item found in collected manifest but not present in source manifest"
                    });
                }
            }

            // Check hash mismatches if required
            if (_options.RequireHashMatch)
            {
                foreach (var sourceItem in sourceItems)
                {
                    var key = sourceItem.GetPrimaryKey();
                    if (collectedDict.TryGetValue(key, out var collectedItem))
                    {
                        if (!string.IsNullOrEmpty(sourceItem.Sha256) &&
                            !string.IsNullOrEmpty(collectedItem.Sha256) &&
                            !sourceItem.Sha256.Equals(collectedItem.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            result.HashMismatches.Add(new DiscrepancyItem
                            {
                                Key = key,
                                Type = "HashMismatch",
                                Custodian = sourceItem.Custodian,
                                DriveId = sourceItem.DriveId,
                                ItemId = sourceItem.ItemId,
                                Path = sourceItem.Path,
                                Size = sourceItem.Size,
                                LastModified = sourceItem.LastModified,
                                SourceSha256 = sourceItem.Sha256,
                                CollectedSha256 = collectedItem.Sha256,
                                Reason = "SHA-256 hash mismatch between source and collected item"
                            });
                        }
                    }
                }
            }

            // Update counts
            result.MissedCount = result.Missed.Count;
            result.ExtraCount = result.Extras.Count;
            result.HashMismatchCount = result.HashMismatches.Count;

            _logger.LogInformation("Reconciliation analysis complete. Missed: {Missed}, Extra: {Extra}, Hash mismatches: {HashMismatch} | CorrelationId: {CorrelationId}",
                result.MissedCount, result.ExtraCount, result.HashMismatchCount, correlationId);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Generate reconciliation report CSV
        /// </summary>
        private async Task<string> GenerateReportAsync(ReconciliationResult result, string correlationId)
        {
            var reportFileName = $"recon_report_{result.JobId}.csv";
            var reportPath = Path.Combine(_options.ReportsPath, reportFileName);

            // Ensure directory exists
            Directory.CreateDirectory(_options.ReportsPath);

            var lines = new List<string>();

            // Add header
            lines.Add("Section,Key,Type,Custodian,DriveId,ItemId,Path,Size,LastModified,SourceSha256,CollectedSha256,Reason,DetectedUtc");

            // Add missed items
            foreach (var item in result.Missed)
            {
                lines.Add($"Missed,{EscapeCsv(item.Key)},Missed,{EscapeCsv(item.Custodian)},{EscapeCsv(item.DriveId ?? "")},{EscapeCsv(item.ItemId ?? "")},{EscapeCsv(item.Path)},{item.Size},{item.LastModified:yyyy-MM-ddTHH:mm:ssZ},{EscapeCsv(item.SourceSha256 ?? "")},{EscapeCsv(item.CollectedSha256 ?? "")},{EscapeCsv(item.Reason)},{item.DetectedUtc:yyyy-MM-ddTHH:mm:ssZ}");
            }

            // Add extra items
            foreach (var item in result.Extras)
            {
                lines.Add($"Extras,{EscapeCsv(item.Key)},Extra,{EscapeCsv(item.Custodian)},{EscapeCsv(item.DriveId ?? "")},{EscapeCsv(item.ItemId ?? "")},{EscapeCsv(item.Path)},{item.Size},{item.LastModified:yyyy-MM-ddTHH:mm:ssZ},{EscapeCsv(item.SourceSha256 ?? "")},{EscapeCsv(item.CollectedSha256 ?? "")},{EscapeCsv(item.Reason)},{item.DetectedUtc:yyyy-MM-ddTHH:mm:ssZ}");
            }

            // Add hash mismatches
            foreach (var item in result.HashMismatches)
            {
                lines.Add($"HashMismatches,{EscapeCsv(item.Key)},HashMismatch,{EscapeCsv(item.Custodian)},{EscapeCsv(item.DriveId ?? "")},{EscapeCsv(item.ItemId ?? "")},{EscapeCsv(item.Path)},{item.Size},{item.LastModified:yyyy-MM-ddTHH:mm:ssZ},{EscapeCsv(item.SourceSha256 ?? "")},{EscapeCsv(item.CollectedSha256 ?? "")},{EscapeCsv(item.Reason)},{item.DetectedUtc:yyyy-MM-ddTHH:mm:ssZ}");
            }

            // Add expected skips
            foreach (var item in result.ExpectedSkips)
            {
                lines.Add($"ExpectedSkips,{EscapeCsv(item.Key)},ExpectedSkip,{EscapeCsv(item.Custodian)},{EscapeCsv(item.DriveId ?? "")},{EscapeCsv(item.ItemId ?? "")},{EscapeCsv(item.Path)},{item.Size},{item.LastModified:yyyy-MM-ddTHH:mm:ssZ},{EscapeCsv(item.SourceSha256 ?? "")},{EscapeCsv(item.CollectedSha256 ?? "")},{EscapeCsv(item.Reason)},{item.DetectedUtc:yyyy-MM-ddTHH:mm:ssZ}");
            }

            // Add summary row
            var sizeDeltaPct = result.SourceTotalBytes == 0 ? 0 : Math.Abs(result.SizeDeltaBytes) / (double)result.SourceTotalBytes * 100;
            var extrasPct = result.SourceCount == 0 ? 0 : (double)result.ExtraCount / result.SourceCount * 100;
            
            lines.Add($"Summary,SUMMARY,Summary,{EscapeCsv(result.Custodian)},,,,,,,,\"SourceCount:{result.SourceCount} CollectedCount:{result.CollectedCount} MissedCount:{result.MissedCount} ExtraCount:{result.ExtraCount} HashMismatchCount:{result.HashMismatchCount} SourceBytes:{result.SourceTotalBytes} CollectedBytes:{result.CollectedTotalBytes} SizeDeltaBytes:{result.SizeDeltaBytes} SizeDeltaPct:{sizeDeltaPct:F2}% ExtrasPct:{extrasPct:F2}% CardinalityPassed:{result.CardinalityPassed} ExtrasPassed:{result.ExtrasPassed} SizePassed:{result.SizePassed} HashPassed:{result.HashPassed} OverallPassed:{result.OverallPassed} SizeTolerancePct:{result.SizeTolerancePct}% ExtraTolerancePct:{result.ExtraTolerancePct}% RequireHashMatch:{result.RequireHashMatch}\",{result.ProcessedUtc:yyyy-MM-ddTHH:mm:ssZ}");

            await File.WriteAllLinesAsync(reportPath, lines);

            _logger.LogInformation("Generated reconciliation report: {ReportPath} | CorrelationId: {CorrelationId}",
                reportPath, correlationId);

            return reportPath;
        }

        /// <summary>
        /// Escape CSV field
        /// </summary>
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        /// <summary>
        /// Log reconciliation result for compliance
        /// </summary>
        private async Task LogReconciliationResultAsync(ReconciliationResult result, string correlationId)
        {
            _complianceLogger.LogAudit(
                "ReconciliationCompleted",
                new
                {
                    JobId = result.JobId,
                    SourceCount = result.SourceCount,
                    CollectedCount = result.CollectedCount,
                    MissedCount = result.MissedCount,
                    ExtraCount = result.ExtraCount,
                    HashMismatchCount = result.HashMismatchCount,
                    SourceTotalBytes = result.SourceTotalBytes,
                    CollectedTotalBytes = result.CollectedTotalBytes,
                    SizeDeltaBytes = result.SizeDeltaBytes,
                    OverallPassed = result.OverallPassed,
                    CardinalityPassed = result.CardinalityPassed,
                    ExtrasPassed = result.ExtrasPassed,
                    SizePassed = result.SizePassed,
                    HashPassed = result.HashPassed,
                    SizeTolerancePct = result.SizeTolerancePct,
                    ExtraTolerancePct = result.ExtraTolerancePct,
                    RequireHashMatch = result.RequireHashMatch,
                    ReportPath = result.ReportPath,
                    ProcessedUtc = result.ProcessedUtc
                },
                result.Custodian,
                correlationId);

            await Task.CompletedTask;
        }
    }
}
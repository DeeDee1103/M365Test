using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HybridGraphCollectorWorker.Models;
using HybridGraphCollectorWorker.Services;
using EDiscovery.Shared.Services;

namespace HybridGraphCollectorWorker.Workers
{
    /// <summary>
    /// Background worker for reconciliation operations
    /// </summary>
    public class ReconcileWorker : BackgroundService
    {
        private readonly ILogger<ReconcileWorker> _logger;
        private readonly ReconcileOptions _options;
        private readonly Reconciler _reconciler;
        private readonly EDiscoveryApiClient _apiClient;
        private readonly ComplianceLogger _complianceLogger;
        private readonly IServiceProvider _serviceProvider;

        public ReconcileWorker(
            ILogger<ReconcileWorker> logger,
            IOptions<ReconcileOptions> options,
            Reconciler reconciler,
            EDiscoveryApiClient apiClient,
            ComplianceLogger complianceLogger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _options = options.Value;
            _reconciler = reconciler;
            _apiClient = apiClient;
            _complianceLogger = complianceLogger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReconcileWorker started");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ProcessPendingReconciliationsAsync(stoppingToken);
                    
                    // Wait before next check
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ReconcileWorker is stopping due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReconcileWorker encountered an error");
            }

            _logger.LogInformation("ReconcileWorker stopped");
        }

        /// <summary>
        /// Process pending reconciliation requests
        /// </summary>
        private async Task ProcessPendingReconciliationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Check for completed jobs that need reconciliation
                // This would typically check a queue or database for reconciliation requests
                // For now, we'll monitor the file system for completion markers

                await CheckForCompletedCollectionsAsync(cancellationToken);
                await CheckForCompletedGdcFetchesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending reconciliations");
            }
        }

        /// <summary>
        /// Check for completed Graph API collections that need reconciliation
        /// </summary>
        private async Task CheckForCompletedCollectionsAsync(CancellationToken cancellationToken)
        {
            var collectedManifestPattern = Path.Combine(_options.CollectedManifestPath, "*_collected.csv");
            var collectedFiles = Directory.GetFiles(_options.CollectedManifestPath, "*_collected.csv", SearchOption.TopDirectoryOnly);

            foreach (var collectedFile in collectedFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(collectedFile);
                    var parts = fileName.Split('_');
                    
                    if (parts.Length >= 3 && parts[^1] == "collected")
                    {
                        var jobId = parts[^2];
                        var custodian = string.Join("_", parts.Take(parts.Length - 2));

                        // Check if reconciliation already completed
                        var reportPath = Path.Combine(_options.ReportsPath, $"recon_report_{jobId}.csv");
                        if (File.Exists(reportPath))
                        {
                            continue; // Already reconciled
                        }

                        // Find corresponding source manifest
                        var sourceFile = await FindSourceManifestAsync(custodian, jobId);
                        if (string.IsNullOrEmpty(sourceFile))
                        {
                            _logger.LogWarning("No source manifest found for custodian {Custodian}, job {JobId}", 
                                custodian, jobId);
                            continue;
                        }

                        // Perform reconciliation
                        await PerformReconciliationAsync(custodian, jobId, sourceFile, collectedFile, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing collected file {File}", collectedFile);
                }
            }
        }

        /// <summary>
        /// Check for completed GDC binary fetches that need reconciliation
        /// </summary>
        private async Task CheckForCompletedGdcFetchesAsync(CancellationToken cancellationToken)
        {
            // Similar logic for GDC binary fetch completions
            // This would check for GDC manifest files and their corresponding collected binary manifests
            await Task.CompletedTask;
        }

        /// <summary>
        /// Find source manifest for a custodian and job
        /// </summary>
        private async Task<string?> FindSourceManifestAsync(string custodian, string jobId)
        {
            await Task.Delay(1); // Minimal async operation

            var patterns = new[]
            {
                $"{custodian}_{jobId}_source.csv",
                $"{custodian}_{jobId}_source.json",
                $"{custodian}_source.csv",
                $"{custodian}_source.json",
                $"source_{jobId}.csv",
                $"source_{jobId}.json"
            };

            foreach (var pattern in patterns)
            {
                var filePath = Path.Combine(_options.SourceManifestPath, pattern);
                if (File.Exists(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }

        /// <summary>
        /// Perform reconciliation and report results
        /// </summary>
        private async Task PerformReconciliationAsync(
            string custodian,
            string jobId,
            string sourceFile,
            string collectedFile,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting reconciliation for custodian {Custodian}, job {JobId}", 
                    custodian, jobId);

                // Perform reconciliation
                var result = await _reconciler.ReconcileAsync(custodian, jobId, sourceFile, collectedFile, cancellationToken);

                // Report results to API
                ReportReconciliationResults(result);

                _logger.LogInformation("Reconciliation completed for custodian {Custodian}, job {JobId}. Result: {Result}",
                    custodian, jobId, result.OverallPassed ? "PASS" : "FAIL");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing reconciliation for custodian {Custodian}, job {JobId}",
                    custodian, jobId);
            }
        }

        /// <summary>
        /// Report reconciliation results to the Intake API
        /// </summary>
        private void ReportReconciliationResults(ReconciliationResult result)
        {
            try
            {
                var stats = new ReconciliationStats
                {
                    ReconMissedCount = result.MissedCount,
                    ReconExtraCount = result.ExtraCount,
                    ReconHashMismatchCount = result.HashMismatchCount,
                    ReconSourceBytes = result.SourceTotalBytes,
                    ReconCollectedBytes = result.CollectedTotalBytes,
                    ReconPassed = result.OverallPassed,
                    ReconReportPath = result.ReportPath,
                    ReconProcessedUtc = result.ProcessedUtc
                };

                // This would POST to /api/jobs/reconcile or update existing job
                _logger.LogInformation("Reporting reconciliation stats for job {JobId}: {Stats}",
                    result.JobId, System.Text.Json.JsonSerializer.Serialize(stats));

                // Log compliance event
                _complianceLogger.LogAudit(
                    "ReconciliationReported",
                    stats,
                    result.Custodian,
                    result.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting reconciliation results for job {JobId}", result.JobId);
            }
        }

        /// <summary>
        /// Run reconciliation on demand (CLI support)
        /// </summary>
        public async Task<ReconciliationResult> RunReconciliationAsync(
            string custodian,
            string jobId,
            string sourceManifestPath,
            string collectedManifestPath,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Running on-demand reconciliation for custodian {Custodian}, job {JobId}",
                custodian, jobId);

            var result = await _reconciler.ReconcileAsync(custodian, jobId, sourceManifestPath, collectedManifestPath, cancellationToken);

            if (!_options.DryRun)
            {
                ReportReconciliationResults(result);
            }

            return result;
        }
    }
}
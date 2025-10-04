using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using EDiscovery.Shared.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HybridGraphCollectorWorker.Services;

public class GraphCollectorService : IGraphCollectorService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphCollectorService> _logger;
    private readonly string _outputBasePath;

    public GraphCollectorService(IConfiguration configuration, ILogger<GraphCollectorService> logger)
    {
        _logger = logger;
        _outputBasePath = configuration["OutputPath"] ?? "./output";

        // Configure Graph Client with Client Secret for POC
        var tenantId = configuration["AzureAd:TenantId"];
        var clientId = configuration["AzureAd:ClientId"];
        var clientSecret = configuration["AzureAd:ClientSecret"];

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential);
    }

    public async Task<CollectionResult> CollectEmailAsync(CollectionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting email collection for custodian: {CustodianEmail}", request.CustodianEmail);
        
        var result = new CollectionResult();
        var retryPolicy = new RetryPolicy(_logger);

        try
        {
            // Get user mailbox
            var user = await retryPolicy.ExecuteAsync(async () =>
                await _graphClient.Users[request.CustodianEmail].GetAsync(cancellationToken: cancellationToken));

            if (user == null)
            {
                result.ErrorMessage = $"User {request.CustodianEmail} not found";
                return result;
            }

            // Build filter query
            var filter = BuildEmailFilter(request);
            
            // Get messages with retry logic
            var messages = await retryPolicy.ExecuteAsync(async () =>
                await _graphClient.Users[request.CustodianEmail].Messages
                    .GetAsync(requestConfiguration =>
                    {
                        if (!string.IsNullOrEmpty(filter))
                            requestConfiguration.QueryParameters.Filter = filter;
                        requestConfiguration.QueryParameters.Top = 1000; // Batch size
                        requestConfiguration.QueryParameters.Select = new[]
                        {
                            "id", "subject", "from", "toRecipients", "ccRecipients", 
                            "receivedDateTime", "sentDateTime", "hasAttachments", "bodyPreview"
                        };
                    }, cancellationToken: cancellationToken));

            if (messages?.Value != null)
            {
                foreach (var message in messages.Value)
                {
                    var collectedItem = await ProcessEmailMessage(message, request, cancellationToken);
                    if (collectedItem != null)
                    {
                        result.Items.Add(collectedItem);
                        result.TotalSizeBytes += collectedItem.SizeBytes;
                    }
                }
            }

            result.TotalItemCount = result.Items.Count;
            result.IsSuccessful = true;
            result.ManifestHash = GenerateManifestHash(result.Items);

            _logger.LogInformation("Email collection completed. Collected {ItemCount} items, {SizeBytes} bytes", 
                result.TotalItemCount, result.TotalSizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting email for custodian: {CustodianEmail}", request.CustodianEmail);
            result.ErrorMessage = ex.Message;
            result.IsSuccessful = false;
        }

        return result;
    }

    public async Task<CollectionResult> CollectOneDriveAsync(CollectionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OneDrive collection for custodian: {CustodianEmail}", request.CustodianEmail);
        
        var result = new CollectionResult();
        var retryPolicy = new RetryPolicy(_logger);

        try
        {
            // Get user's drive
            var drive = await retryPolicy.ExecuteAsync(async () =>
                await _graphClient.Users[request.CustodianEmail].Drive.GetAsync(cancellationToken: cancellationToken));

            if (drive == null)
            {
                result.ErrorMessage = $"OneDrive not found for user {request.CustodianEmail}";
                return result;
            }

            // Get files from root and search if keywords provided
            await CollectDriveItems(drive.Id!, request, result, retryPolicy, cancellationToken);

            result.TotalItemCount = result.Items.Count;
            result.IsSuccessful = true;
            result.ManifestHash = GenerateManifestHash(result.Items);

            _logger.LogInformation("OneDrive collection completed. Collected {ItemCount} items, {SizeBytes} bytes", 
                result.TotalItemCount, result.TotalSizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting OneDrive for custodian: {CustodianEmail}", request.CustodianEmail);
            result.ErrorMessage = ex.Message;
            result.IsSuccessful = false;
        }

        return result;
    }

    public async Task<CollectionResult> CollectSharePointAsync(CollectionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting SharePoint collection for custodian: {CustodianEmail}", request.CustodianEmail);
        
        // For POC, this would collect from user's associated SharePoint sites
        var result = new CollectionResult();
        
        try
        {
            // Placeholder for SharePoint collection logic
            // In production, this would enumerate sites, document libraries, etc.
            result.IsSuccessful = true;
            result.ManifestHash = GenerateManifestHash(result.Items);
            
            _logger.LogInformation("SharePoint collection completed (placeholder)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting SharePoint for custodian: {CustodianEmail}", request.CustodianEmail);
            result.ErrorMessage = ex.Message;
            result.IsSuccessful = false;
        }

        return result;
    }

    public async Task<CollectionResult> CollectTeamsAsync(CollectionRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Teams collection for custodian: {CustodianEmail}", request.CustodianEmail);
        
        // For POC, this would collect Teams messages and files
        var result = new CollectionResult();
        
        try
        {
            // Placeholder for Teams collection logic
            // In production, this would collect chat messages, channel messages, files, etc.
            result.IsSuccessful = true;
            result.ManifestHash = GenerateManifestHash(result.Items);
            
            _logger.LogInformation("Teams collection completed (placeholder)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting Teams for custodian: {CustodianEmail}", request.CustodianEmail);
            result.ErrorMessage = ex.Message;
            result.IsSuccessful = false;
        }

        return result;
    }

    private string BuildEmailFilter(CollectionRequest request)
    {
        var filters = new List<string>();

        if (request.StartDate.HasValue)
            filters.Add($"receivedDateTime ge {request.StartDate.Value:yyyy-MM-ddTHH:mm:ssZ}");

        if (request.EndDate.HasValue)
            filters.Add($"receivedDateTime le {request.EndDate.Value:yyyy-MM-ddTHH:mm:ssZ}");

        if (request.Keywords.Any())
        {
            var keywordFilters = request.Keywords
                .Select(k => $"contains(subject,'{k}') or contains(bodyPreview,'{k}')");
            filters.Add($"({string.Join(" or ", keywordFilters)})");
        }

        return string.Join(" and ", filters);
    }

    private async Task<CollectedItem?> ProcessEmailMessage(Message message, CollectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var item = new CollectedItem
            {
                ItemId = message.Id ?? string.Empty,
                ItemType = "Email",
                Subject = message.Subject,
                From = message.From?.EmailAddress?.Address,
                To = string.Join(";", message.ToRecipients?.Select(r => r.EmailAddress?.Address ?? "") ?? Array.Empty<string>()),
                ItemDate = message.ReceivedDateTime?.DateTime,
                SizeBytes = EstimateMessageSize(message)
            };

            // Generate file path and save message
            var fileName = SanitizeFileName($"{message.Id}_{message.Subject}.json");
            var filePath = Path.Combine(_outputBasePath, request.CustodianEmail, "Email", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            
            var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, messageJson, cancellationToken);

            item.FilePath = filePath;
            item.Sha256Hash = ComputeSha256Hash(messageJson);

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing email message: {MessageId}", message.Id);
            return null;
        }
    }

    private async Task CollectDriveItems(string driveId, CollectionRequest request, CollectionResult result, RetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        var items = await retryPolicy.ExecuteAsync(async () =>
            await _graphClient.Drives[driveId].Items["root"].Children
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                }, cancellationToken: cancellationToken));

        if (items?.Value != null)
        {
            foreach (var item in items.Value)
            {
                if (item.File != null) // It's a file, not a folder
                {
                    var collectedItem = await ProcessDriveItem(item, request, cancellationToken);
                    if (collectedItem != null)
                    {
                        result.Items.Add(collectedItem);
                        result.TotalSizeBytes += collectedItem.SizeBytes;
                    }
                }
            }
        }
    }

    private async Task<CollectedItem?> ProcessDriveItem(DriveItem driveItem, CollectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var item = new CollectedItem
            {
                ItemId = driveItem.Id ?? string.Empty,
                ItemType = "File",
                Subject = driveItem.Name,
                ItemDate = driveItem.LastModifiedDateTime?.DateTime,
                SizeBytes = driveItem.Size ?? 0
            };

            // For POC, just record metadata (not download actual file content)
            var fileName = SanitizeFileName($"{driveItem.Id}_{driveItem.Name}_metadata.json");
            var filePath = Path.Combine(_outputBasePath, request.CustodianEmail, "OneDrive", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            
            var metadata = JsonSerializer.Serialize(driveItem, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, metadata, cancellationToken);

            item.FilePath = filePath;
            item.Sha256Hash = ComputeSha256Hash(metadata);

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing drive item: {ItemId}", driveItem.Id);
            return null;
        }
    }

    private static long EstimateMessageSize(Message message)
    {
        // Rough estimate based on message properties
        var size = (message.Subject?.Length ?? 0) * 2; // Unicode characters
        size += (message.BodyPreview?.Length ?? 0) * 2;
        size += 1024; // Headers and metadata
        return size;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.Length > 100 ? sanitized[..100] : sanitized; // Limit length
    }

    private static string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateManifestHash(List<CollectedItem> items)
    {
        var manifest = JsonSerializer.Serialize(items.Select(i => new { i.ItemId, i.Sha256Hash }));
        return ComputeSha256Hash(manifest);
    }
}
using EDiscovery.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Enhanced OneDrive delta enumerator with /drive/root/delta support
/// </summary>
public interface IOneDriveDeltaEnumerator
{
    /// <summary>
    /// Perform delta query for OneDrive items
    /// </summary>
    Task<DeltaQueryResult> EnumerateDeltaAsync(string custodianEmail, string? deltaToken = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of OneDrive delta enumeration
/// </summary>
public class OneDriveDeltaEnumerator : IOneDriveDeltaEnumerator
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<OneDriveDeltaEnumerator> _logger;

    public OneDriveDeltaEnumerator(GraphServiceClient graphClient, ILogger<OneDriveDeltaEnumerator> logger)
    {
        _graphClient = graphClient;
        _logger = logger;
    }

    public async Task<DeltaQueryResult> EnumerateDeltaAsync(string custodianEmail, string? deltaToken = null, CancellationToken cancellationToken = default)
    {
        var result = new DeltaQueryResult
        {
            Items = new List<CollectedItem>(),
            NewDeltaToken = deltaToken,
            HasMorePages = false,
            ProcessedUtc = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting OneDrive delta enumeration for {Custodian} with token: {Token}", 
                custodianEmail, deltaToken ?? "INITIAL");

            // Build the delta request
            var request = _graphClient.Users[custodianEmail].Drive.Root.Delta;
            
            if (!string.IsNullOrEmpty(deltaToken))
            {
                // Continue from existing delta token
                request = request.WithUrl(deltaToken);
            }

            var response = await request.GetAsync(cancellationToken);
            
            if (response?.Value != null)
            {
                foreach (var driveItem in response.Value)
                {
                    // Skip deleted items (they have no content to collect)
                    if (driveItem.Deleted != null)
                    {
                        _logger.LogDebug("Skipping deleted item: {ItemId}", driveItem.Id);
                        continue;
                    }

                    // Skip folders unless specifically requested
                    if (driveItem.Folder != null)
                    {
                        _logger.LogDebug("Skipping folder: {ItemName}", driveItem.Name);
                        continue;
                    }

                    // Only process files
                    if (driveItem.File != null)
                    {
                        var collectedItem = new CollectedItem
                        {
                            DriveItemId = driveItem.Id ?? string.Empty,
                            Name = driveItem.Name ?? "Unknown",
                            Path = driveItem.ParentReference?.Path ?? "/",
                            Size = driveItem.Size ?? 0,
                            LastModified = driveItem.LastModifiedDateTime?.DateTime ?? DateTime.MinValue,
                            ContentType = driveItem.File.MimeType ?? "application/octet-stream",
                            ItemType = CollectedItemType.OneDriveFile,
                            CustodianEmail = custodianEmail,
                            CollectedDate = DateTime.UtcNow,
                            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                CreatedDateTime = driveItem.CreatedDateTime,
                                WebUrl = driveItem.WebUrl,
                                ETag = driveItem.ETag,
                                CTag = driveItem.CTag
                            })
                        };

                        result.Items.Add(collectedItem);
                    }
                }

                // Check for next page
                if (response.OdataNextLink != null)
                {
                    result.NewDeltaToken = response.OdataNextLink;
                    result.HasMorePages = true;
                }
                else if (response.OdataDeltaLink != null)
                {
                    result.NewDeltaToken = response.OdataDeltaLink;
                    result.HasMorePages = false;
                }
            }

            result.ItemCount = result.Items.Count;
            result.TotalSizeBytes = result.Items.Sum(i => i.Size);

            _logger.LogInformation("OneDrive delta enumeration completed for {Custodian}: {ItemCount} items, {SizeBytes:N0} bytes",
                custodianEmail, result.ItemCount, result.TotalSizeBytes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OneDrive delta enumeration for {Custodian}", custodianEmail);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }
}

/// <summary>
/// Enhanced Mail delta enumerator with /me/mailFolders/inbox/messages/delta support
/// </summary>
public interface IMailDeltaEnumerator
{
    /// <summary>
    /// Perform delta query for mail items
    /// </summary>
    Task<DeltaQueryResult> EnumerateDeltaAsync(string custodianEmail, string? deltaToken = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Mail delta enumeration
/// </summary>
public class MailDeltaEnumerator : IMailDeltaEnumerator
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<MailDeltaEnumerator> _logger;

    public MailDeltaEnumerator(GraphServiceClient graphClient, ILogger<MailDeltaEnumerator> logger)
    {
        _graphClient = graphClient;
        _logger = logger;
    }

    public async Task<DeltaQueryResult> EnumerateDeltaAsync(string custodianEmail, string? deltaToken = null, CancellationToken cancellationToken = default)
    {
        var result = new DeltaQueryResult
        {
            Items = new List<CollectedItem>(),
            NewDeltaToken = deltaToken,
            HasMorePages = false,
            ProcessedUtc = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting Mail delta enumeration for {Custodian} with token: {Token}", 
                custodianEmail, deltaToken ?? "INITIAL");

            // Build the delta request for inbox messages
            var request = _graphClient.Users[custodianEmail].MailFolders["inbox"].Messages.Delta;
            
            if (!string.IsNullOrEmpty(deltaToken))
            {
                // Continue from existing delta token
                request = request.WithUrl(deltaToken);
            }

            var response = await request.GetAsync(cancellationToken);
            
            if (response?.Value != null)
            {
                foreach (var message in response.Value)
                {
                    // Skip deleted messages
                    if (message.IsDeleted == true)
                    {
                        _logger.LogDebug("Skipping deleted message: {MessageId}", message.Id);
                        continue;
                    }

                    var collectedItem = new CollectedItem
                    {
                        DriveItemId = message.Id ?? string.Empty,
                        Name = message.Subject ?? "No Subject",
                        Path = "/Inbox",
                        Size = message.Body?.Content?.Length ?? 0,
                        LastModified = message.LastModifiedDateTime?.DateTime ?? DateTime.MinValue,
                        ContentType = "message/rfc822",
                        ItemType = CollectedItemType.Email,
                        CustodianEmail = custodianEmail,
                        CollectedDate = DateTime.UtcNow,
                        MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            From = message.From?.EmailAddress?.Address,
                            To = message.ToRecipients?.Select(r => r.EmailAddress?.Address),
                            Cc = message.CcRecipients?.Select(r => r.EmailAddress?.Address),
                            Bcc = message.BccRecipients?.Select(r => r.EmailAddress?.Address),
                            SentDateTime = message.SentDateTime,
                            ReceivedDateTime = message.ReceivedDateTime,
                            Importance = message.Importance?.ToString(),
                            HasAttachments = message.HasAttachments,
                            ConversationId = message.ConversationId,
                            InternetMessageId = message.InternetMessageId
                        })
                    };

                    result.Items.Add(collectedItem);
                }

                // Check for next page
                if (response.OdataNextLink != null)
                {
                    result.NewDeltaToken = response.OdataNextLink;
                    result.HasMorePages = true;
                }
                else if (response.OdataDeltaLink != null)
                {
                    result.NewDeltaToken = response.OdataDeltaLink;
                    result.HasMorePages = false;
                }
            }

            result.ItemCount = result.Items.Count;
            result.TotalSizeBytes = result.Items.Sum(i => i.Size);

            _logger.LogInformation("Mail delta enumeration completed for {Custodian}: {ItemCount} items, {SizeBytes:N0} bytes",
                custodianEmail, result.ItemCount, result.TotalSizeBytes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Mail delta enumeration for {Custodian}", custodianEmail);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }
}
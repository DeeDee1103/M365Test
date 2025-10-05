using EDiscovery.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using EDiscovery.Shared.Configuration;

namespace EDiscovery.Shared.Services;

public interface IGraphDataConnectService
{
    Task<CollectionResult> TriggerCollectionAsync(CollectionRequest request, CancellationToken cancellationToken = default);
    Task<GdcPipelineStatus> GetPipelineStatusAsync(string pipelineRunId, CancellationToken cancellationToken = default);
}

public class GraphDataConnectService : IGraphDataConnectService
{
    private readonly ILogger<GraphDataConnectService> _logger;
    private readonly IComplianceLogger _complianceLogger;
    private readonly GdcOptions _options;
    private readonly ServiceBusClient? _serviceBusClient;

    public GraphDataConnectService(
        ILogger<GraphDataConnectService> logger,
        IComplianceLogger complianceLogger,
        IOptions<GdcOptions> options)
    {
        _logger = logger;
        _complianceLogger = complianceLogger;
        _options = options.Value;

        // Initialize Service Bus client if connection string is provided
        if (!string.IsNullOrEmpty(_options.ServiceBus?.ConnectionString))
        {
            try
            {
                _serviceBusClient = new ServiceBusClient(_options.ServiceBus.ConnectionString);
                _logger.LogInformation("Graph Data Connect Service Bus client initialized for queue: {QueueName}", 
                    _options.ServiceBus.AdfTriggerQueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Service Bus client for Graph Data Connect");
            }
        }
        else
        {
            _logger.LogWarning("Graph Data Connect Service Bus connection not configured - running in stub mode");
        }
    }

    public async Task<CollectionResult> TriggerCollectionAsync(CollectionRequest request, CancellationToken cancellationToken = default)
    {
        var correlationId = _complianceLogger.CreateCorrelationId();
        
        _logger.LogInformation("Triggering Graph Data Connect collection for custodian: {CustodianEmail} | CorrelationId: {CorrelationId}", 
            request.CustodianEmail, correlationId);

        _complianceLogger.LogAudit("GdcCollectionTriggered", new 
        { 
            CustodianEmail = request.CustodianEmail,
            JobType = request.JobType.ToString(),
            DateRange = new { request.StartDate, request.EndDate },
            TriggerType = "AzureDataFactory"
        }, request.CustodianEmail, correlationId);

        try
        {
            var pipelineRunId = Guid.NewGuid().ToString("N");
            
            // Create GDC trigger message
            var triggerMessage = new GdcTriggerMessage
            {
                PipelineRunId = pipelineRunId,
                CollectionRequest = request,
                TriggeredAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                Priority = DeterminePriority(request),
                EstimatedDuration = EstimateDuration(request),
                RetryPolicy = new GdcRetryPolicy
                {
                    MaxRetries = 3,
                    RetryDelayMinutes = 15,
                    ExponentialBackoff = true
                }
            };

            if (_serviceBusClient != null && !string.IsNullOrEmpty(_options.ServiceBus?.AdfTriggerQueueName))
            {
                // Send message to Service Bus queue for ADF trigger
                await SendToServiceBusAsync(triggerMessage, cancellationToken);
            }
            else
            {
                // Stub mode - just log the message
                await LogStubMessage(triggerMessage, cancellationToken);
            }

            _complianceLogger.LogAudit("GdcCollectionTriggered", new
            {
                PipelineRunId = pipelineRunId,
                TriggerMessage = triggerMessage,
                ServiceBusEnabled = _serviceBusClient != null
            }, request.CustodianEmail, correlationId);

            return new CollectionResult
            {
                IsSuccessful = true,
                CollectedItemsCount = 0, // Will be updated by pipeline completion
                CollectedSizeBytes = 0,
                CollectionMetadata = new Dictionary<string, object>
                {
                    ["PipelineRunId"] = pipelineRunId,
                    ["TriggerMethod"] = "AzureDataFactory",
                    ["Status"] = "Triggered",
                    ["CorrelationId"] = correlationId,
                    ["EstimatedDuration"] = triggerMessage.EstimatedDuration.ToString(),
                    ["Priority"] = triggerMessage.Priority.ToString()
                }
            };
        }
        catch (Exception ex)
        {
            _complianceLogger.LogError(ex, "GdcCollectionTrigger", new
            {
                CustodianEmail = request.CustodianEmail,
                JobType = request.JobType.ToString()
            }, correlationId);

            return new CollectionResult
            {
                IsSuccessful = false,
                ErrorMessage = $"Failed to trigger Graph Data Connect collection: {ex.Message}"
            };
        }
    }

    public async Task<GdcPipelineStatus> GetPipelineStatusAsync(string pipelineRunId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking pipeline status for run: {PipelineRunId}", pipelineRunId);

        // In stub mode, return a simulated status
        // In production, this would query Azure Data Factory REST API
        await Task.Delay(100, cancellationToken);

        return new GdcPipelineStatus
        {
            PipelineRunId = pipelineRunId,
            Status = GdcPipelineState.Running,
            Progress = Random.Shared.Next(10, 90),
            Message = "Data extraction in progress",
            LastUpdated = DateTime.UtcNow,
            EstimatedCompletion = DateTime.UtcNow.AddMinutes(Random.Shared.Next(30, 120))
        };
    }

    private async Task SendToServiceBusAsync(GdcTriggerMessage message, CancellationToken cancellationToken)
    {
        var sender = _serviceBusClient!.CreateSender(_options.ServiceBus!.AdfTriggerQueueName);
        
        try
        {
            var messageBody = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                MessageId = message.PipelineRunId,
                CorrelationId = message.CorrelationId,
                Subject = $"GDC-Collection-{message.CollectionRequest.JobType}",
                TimeToLive = TimeSpan.FromHours(24) // Message expires after 24 hours
            };

            // Add custom properties for message routing
            serviceBusMessage.ApplicationProperties.Add("JobType", message.CollectionRequest.JobType.ToString());
            serviceBusMessage.ApplicationProperties.Add("CustodianEmail", message.CollectionRequest.CustodianEmail);
            serviceBusMessage.ApplicationProperties.Add("Priority", message.Priority.ToString());
            serviceBusMessage.ApplicationProperties.Add("TriggeredAt", message.TriggeredAt.ToString("O"));

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogInformation("GDC trigger message sent to Service Bus queue: {QueueName} | MessageId: {MessageId}", 
                _options.ServiceBus.AdfTriggerQueueName, serviceBusMessage.MessageId);
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    private async Task LogStubMessage(GdcTriggerMessage message, CancellationToken cancellationToken)
    {
        var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        _logger.LogInformation("GDC Stub Mode - Would send trigger message to ADF:\n{MessageJson}", messageJson);
        
        // Simulate processing delay
        await Task.Delay(Random.Shared.Next(500, 2000), cancellationToken);
    }

    private GdcPriority DeterminePriority(CollectionRequest request)
    {
        // Determine priority based on job characteristics
        return request.JobType switch
        {
            CollectionJobType.Email when request.EndDate.Value.Subtract(request.StartDate.Value).Days <= 7 => GdcPriority.High,
            CollectionJobType.Teams => GdcPriority.High, // Teams data often time-sensitive
            CollectionJobType.Mixed => GdcPriority.Medium,
            _ => GdcPriority.Normal
        };
    }

    private TimeSpan EstimateDuration(CollectionRequest request)
    {
        // Estimate based on job type and date range
        var dateRangeDays = request.EndDate.Value.Subtract(request.StartDate.Value).Days;
        
        return request.JobType switch
        {
            CollectionJobType.Email => TimeSpan.FromMinutes(Math.Max(30, dateRangeDays * 2)),
            CollectionJobType.OneDrive => TimeSpan.FromMinutes(Math.Max(45, dateRangeDays * 3)),
            CollectionJobType.SharePoint => TimeSpan.FromMinutes(Math.Max(60, dateRangeDays * 4)),
            CollectionJobType.Teams => TimeSpan.FromMinutes(Math.Max(20, dateRangeDays * 1.5)),
            CollectionJobType.Mixed => TimeSpan.FromMinutes(Math.Max(90, dateRangeDays * 5)),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    public void Dispose()
    {
        _serviceBusClient?.DisposeAsync().AsTask().Wait();
    }
}
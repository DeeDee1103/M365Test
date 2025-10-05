namespace EDiscovery.Shared.Configuration;

public class GdcOptions
{
    public const string SectionName = "GraphDataConnect";

    /// <summary>
    /// Azure Data Factory configuration
    /// </summary>
    public AdfConfiguration? Adf { get; set; }

    /// <summary>
    /// Service Bus configuration for ADF triggers
    /// </summary>
    public ServiceBusConfiguration? ServiceBus { get; set; }

    /// <summary>
    /// Pipeline execution settings
    /// </summary>
    public PipelineConfiguration Pipeline { get; set; } = new();

    /// <summary>
    /// Output storage configuration
    /// </summary>
    public OutputStorageConfiguration OutputStorage { get; set; } = new();
}

public class AdfConfiguration
{
    /// <summary>
    /// Azure Data Factory resource group name
    /// </summary>
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>
    /// Azure Data Factory name
    /// </summary>
    public string DataFactoryName { get; set; } = string.Empty;

    /// <summary>
    /// Azure subscription ID
    /// </summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Pipeline name for Graph Data Connect collection
    /// </summary>
    public string PipelineName { get; set; } = "GraphDataConnectCollectionPipeline";

    /// <summary>
    /// Timeout for pipeline execution (in minutes)
    /// </summary>
    public int TimeoutMinutes { get; set; } = 480; // 8 hours default

    /// <summary>
    /// Enable pipeline monitoring and alerts
    /// </summary>
    public bool EnableMonitoring { get; set; } = true;
}

public class ServiceBusConfiguration
{
    /// <summary>
    /// Service Bus connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Queue name for ADF pipeline triggers
    /// </summary>
    public string AdfTriggerQueueName { get; set; } = "adf-gdc-triggers";

    /// <summary>
    /// Queue name for pipeline status updates
    /// </summary>
    public string StatusUpdateQueueName { get; set; } = "adf-gdc-status";

    /// <summary>
    /// Dead letter queue for failed messages
    /// </summary>
    public string DeadLetterQueueName { get; set; } = "adf-gdc-deadletter";

    /// <summary>
    /// Message time-to-live in hours
    /// </summary>
    public int MessageTtlHours { get; set; } = 24;

    /// <summary>
    /// Maximum delivery count before moving to dead letter
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 3;
}

public class PipelineConfiguration
{
    /// <summary>
    /// Default priority for pipeline execution
    /// </summary>
    public string DefaultPriority { get; set; } = "Normal";

    /// <summary>
    /// Maximum concurrent pipeline executions
    /// </summary>
    public int MaxConcurrentPipelines { get; set; } = 5;

    /// <summary>
    /// Retry policy settings
    /// </summary>
    public RetryPolicyConfiguration RetryPolicy { get; set; } = new();

    /// <summary>
    /// Enable delta lake format for output
    /// </summary>
    public bool EnableDeltaLake { get; set; } = true;

    /// <summary>
    /// Output file format (Parquet, JSON, CSV)
    /// </summary>
    public string OutputFormat { get; set; } = "Parquet";

    /// <summary>
    /// Compression type for output files
    /// </summary>
    public string CompressionType { get; set; } = "Snappy";
}

public class RetryPolicyConfiguration
{
    /// <summary>
    /// Maximum number of retries
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in minutes
    /// </summary>
    public int InitialDelayMinutes { get; set; } = 15;

    /// <summary>
    /// Use exponential backoff for retry delays
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Maximum retry delay in minutes
    /// </summary>
    public int MaxDelayMinutes { get; set; } = 120;
}

public class OutputStorageConfiguration
{
    /// <summary>
    /// Azure Storage account connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Container name for GDC output data
    /// </summary>
    public string ContainerName { get; set; } = "gdc-collections";

    /// <summary>
    /// Base path within container for organized storage
    /// </summary>
    public string BasePath { get; set; } = "collections/{year}/{month}/{day}";

    /// <summary>
    /// Enable data encryption at rest
    /// </summary>
    public bool EnableEncryption { get; set; } = true;

    /// <summary>
    /// Data retention period in days
    /// </summary>
    public int RetentionDays { get; set; } = 2555; // 7 years

    /// <summary>
    /// Enable immutability (WORM) for compliance
    /// </summary>
    public bool EnableImmutability { get; set; } = true;
}
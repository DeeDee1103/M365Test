# GDC Binary Fetch

## Overview

The GDC Binary Fetch functionality extends the Hybrid eDiscovery Collector to add a post-GDC step that reads Graph Data Connect (GDC) dataset JSON files, calls Microsoft Graph API to download each file's binary content, writes files to the landing zone, and records SHA-256 hashes with manifests per job.

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Azure Data    │    │  GDC Dataset     │    │ GdcFetchWorker  │
│   Factory       │───▶│  JSON Files      │───▶│ (Orchestrator)  │
│   (GDC)         │    │  (Landing Zone)  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Binary Files  │◀───│ GdcBinaryFetcher │◀───│ eDiscovery API  │
│   + Manifests   │    │    (Service)     │    │  Job Creation   │
│ (Landing Zone)  │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## Components

### 1. GdcBinaryFetcher Service

**Location**: `HybridGraphCollectorWorker/Services/GdcBinaryFetcher.cs`

Core service responsible for:

- Reading GDC dataset JSON files (line-delimited or array format)
- Filtering records based on configuration
- Calling Microsoft Graph API to download file binaries
- Computing SHA-256 hashes during download
- Writing files to organized directory structure
- Creating comprehensive manifests (CSV and JSON)

**Key Features**:

- **Parallel Processing**: Configurable concurrency with semaphore control
- **Retry Logic**: Uses existing `RetryPolicyHandler` for Graph API calls
- **Idempotency**: Supports dry-run mode and duplicate detection
- **Error Handling**: Configurable max error threshold
- **Progress Tracking**: Detailed telemetry and logging

### 2. GdcFetchWorker (Orchestrator)

**Location**: `HybridGraphCollectorWorker/Workers/GdcFetchWorker.cs`

Background worker that:

- Monitors configured locations for completed GDC runs
- Detects completion markers (`_SUCCESS`, `*.complete`, JSON datasets)
- Creates jobs in eDiscovery Intake API
- Orchestrates `GdcBinaryFetcher` execution
- Updates job status with results

**Monitoring Methods**:

- **File System Watcher**: Real-time detection for NAS storage
- **Polling Timer**: Periodic checks for new datasets
- **Blob Storage**: Configurable blob monitoring (extensible)

### 3. Configuration Models

**Location**: `HybridGraphCollectorWorker/Models/GdcBinaryFetchOptions.cs`

Comprehensive configuration supporting:

- Input source selection (NAS or Blob)
- Filtering by custodians, file extensions, and modification dates
- Performance tuning (parallelism, error thresholds)
- Dry-run mode for validation

## Configuration

### appsettings.json

```json
{
  "Gdc": {
    "Input": {
      "Kind": "nas",
      "Blob": {
        "ConnectionString": "",
        "Container": "gdc-out",
        "Prefix": "files/"
      },
      "Nas": {
        "Root": "./gdc-out"
      }
    },
    "Filters": {
      "Custodians": ["*"],
      "FileExtensions": ["*"],
      "ModifiedAfter": null
    },
    "Parallelism": 8,
    "MaxErrors": 100,
    "DryRun": false
  }
}
```

### Environment Variable Overrides

All configuration supports environment variable overrides using double underscore notation:

```bash
Gdc__Parallelism=16
Gdc__Input__Blob__Container=custom-gdc-out
Gdc__Input__Nas__Root=/mnt/gdc-data
Gdc__Filters__Custodians__0=user1@company.com
Gdc__Filters__Custodians__1=user2@company.com
Gdc__MaxErrors=50
Gdc__DryRun=true
```

## Required Graph Permissions

The application requires the following Microsoft Graph application permissions:

- **Files.Read.All**: Read files in OneDrive and SharePoint
- **Sites.Read.All**: Access SharePoint sites and document libraries

These permissions are already configured for the existing Graph API functionality.

### Authentication Methods

- **Development**: Client Secret credential
- **Production**: Managed Identity (recommended)

## GDC Dataset JSON Format

The service supports both line-delimited JSON and array JSON formats from GDC datasets:

### Line-Delimited JSON (Preferred)

```json
{"driveId":"b!abc123","id":"def456","name":"document.docx","size":1024,...}
{"driveId":"b!abc123","id":"ghi789","name":"spreadsheet.xlsx","size":2048,...}
```

### Array JSON

```json
[
  {"driveId":"b!abc123","id":"def456","name":"document.docx","size":1024,...},
  {"driveId":"b!abc123","id":"ghi789","name":"spreadsheet.xlsx","size":2048,...}
]
```

### Required Fields

For successful binary fetch, records must contain:

- `driveId` (or `parentReference.driveId`)
- `id` (item ID)
- `name` or `fileName`

### Field Mapping

The service maps various GDC field variations:

- **Drive ID**: `driveId` → `parentReference.driveId`
- **Item ID**: `id` → `itemId`
- **File Name**: `name` → `fileName`
- **Path**: `path` (for organizational purposes)
- **Metadata**: `size`, `lastModifiedDateTime`, `mimeType`

## Output Structure

### Directory Organization

```
output/
├── matter/
│   └── {MatterName}/
│       └── GDC/
│           └── {custodian}/
│               ├── 000001_document.docx
│               ├── 000002_spreadsheet.xlsx
│               └── ...
└── logs/
    └── {MatterName}/
        └── {JobId}/
            ├── manifest.csv
            ├── manifest.json
            └── manifest.sha256
```

### Manifest Format

#### CSV Format (`manifest.csv`)

```csv
Custodian,Kind,DriveId,ItemId,Path,Size,SHA256,StorageUri,CollectedUtc
user@company.com,File,b!abc123,def456,/Documents/document.docx,1024,a1b2c3...,./output/matter/Test/GDC/user@company.com/000001_document.docx,2024-10-06T19:30:00.000Z
```

#### JSON Format (`manifest.json`)

```json
[
  {
    "custodian": "user@company.com",
    "kind": "File",
    "driveId": "b!abc123",
    "itemId": "def456",
    "path": "/Documents/document.docx",
    "size": 1024,
    "sha256": "a1b2c3d4e5f6...",
    "storageUri": "./output/matter/Test/GDC/user@company.com/000001_document.docx",
    "collectedUtc": "2024-10-06T19:30:00.000Z",
    "originalPath": "/Documents/document.docx",
    "mimeType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "lastModifiedDateTime": "2024-10-01T10:15:30.000Z"
  }
]
```

#### Manifest Integrity (`manifest.sha256`)

```
a1b2c3d4e5f6789abcdef123456789abcdef123456789abcdef123456789abcdef
```

## Azure Data Factory Integration

### Pipeline Overview

**Location**: `infra/adf/gdc_to_landing_pipeline.json`

The ADF pipeline provides:

1. **Parameter Validation**: Custodian list and dataset criteria
2. **GDC Execution**: Triggers Graph Data Connect extraction
3. **Format Conversion**: Parquet → JSON (if needed)
4. **Landing Zone Copy**: Moves datasets to monitored location
5. **Completion Notification**: Creates success markers and API callbacks

### Pipeline Parameters

```json
{
  "custodianList": ["user1@company.com", "user2@company.com"],
  "datasetSelection": {
    "startDate": "2024-01-01",
    "endDate": "2024-12-31",
    "contentTypes": ["Files", "OneDrive"],
    "includeSharePoint": true,
    "includeTeams": false
  },
  "outputPath": "gdc-out/files/",
  "matterName": "MyMatter",
  "enableBinaryFetch": true,
  "eDiscoveryApiEndpoint": "https://your-api.azurewebsites.net"
}
```

### Webhook Integration

The pipeline sends completion notifications to:

```
POST /api/jobs/gdc-completed
{
  "pipelineRunId": "abc-123-def",
  "gdcRunId": "gdc-dataset-MyMatter-20241006-193000",
  "matterName": "MyMatter",
  "custodians": ["user@company.com"],
  "outputPath": "gdc-out/files/",
  "status": "Completed",
  "completedAt": "2024-10-06T19:30:00Z",
  "enableBinaryFetch": true
}
```

## Performance and Monitoring

### Telemetry Events

The service emits structured log events for dashboard integration:

```
DATA_COLLECTION: BinaryFetched RecordKey={RecordKey} Size={Size} SHA256={SHA256}
DATA_COLLECTION: SkippedNoId RecordKey={RecordKey} Reason={Reason}
DATA_COLLECTION: Failed RecordKey={RecordKey} Error={Error}
PERFORMANCE: GDC_BINARY_FETCH_COMPLETED JobId={JobId} Downloaded={Count} TotalBytes={Bytes}
CHAIN_OF_CUSTODY: ManifestCreated JobId={JobId} Entries={Count}
```

### Performance Tuning

#### Parallelism Settings

- **Default**: 8 concurrent downloads
- **Recommendation**: 4-16 based on Graph API throttling
- **Monitoring**: Watch for 429 responses and adjust accordingly

#### Memory Optimization

- **Streaming Downloads**: 80KB buffer for large files
- **Hash Computation**: Computed during download (no double I/O)
- **Semaphore Control**: Prevents resource exhaustion

#### Throttling Management

- **Retry Policy**: Exponential backoff with jitter
- **Respect 429**: Honors `Retry-After` headers
- **Circuit Breaker**: Configurable error thresholds

### Dashboard Integration

Compatible with:

- **Grafana**: JSON endpoints for metrics visualization
- **Azure Monitor**: Application Insights integration
- **Splunk**: Structured log ingestion
- **Kusto**: KQL queries on performance logs

## Testing and Validation

### Test Script

Create a sample GDC dataset for testing:

```bash
# Create test directory structure
mkdir -p ./gdc-out/files

# Create sample GDC JSON dataset
cat > ./gdc-out/files/test-dataset.json << 'EOF'
{"driveId":"b!test123","id":"item456","name":"test-document.txt","size":256,"lastModifiedDateTime":"2024-10-06T19:00:00Z"}
{"driveId":"b!test123","id":"item789","name":"test-spreadsheet.xlsx","size":512,"lastModifiedDateTime":"2024-10-06T19:00:00Z"}
EOF

# Run the worker
dotnet run --project HybridGraphCollectorWorker

# Verify outputs
ls -la ./output/matter/DefaultMatter/GDC/
ls -la ./logs/DefaultMatter/
```

### Dry Run Mode

Enable dry-run mode to validate IDs without downloading:

```json
{
  "Gdc": {
    "DryRun": true
  }
}
```

This mode:

- Validates Graph API connectivity
- Checks drive/item ID availability
- Skips actual file downloads
- Creates mock manifests for testing

### Validation Checklist

- [ ] Graph API permissions configured
- [ ] GDC dataset files accessible
- [ ] Output directories writable
- [ ] Manifest files generated correctly
- [ ] SHA-256 hashes computed accurately
- [ ] Job status updated in eDiscovery API
- [ ] Telemetry events logged properly

## Security Considerations

### Data Protection

- **In-Transit**: HTTPS for all Graph API calls
- **At-Rest**: Files written to secure landing zones
- **Integrity**: SHA-256 verification for all downloads

### Access Control

- **Least Privilege**: Minimal Graph permissions required
- **Managed Identity**: Production authentication method
- **Audit Trail**: Complete chain of custody logging

### Compliance

- **Tamper Evidence**: Manifest integrity verification
- **Retention**: Configurable retention policies
- **Immutability**: WORM storage integration ready

## Troubleshooting

### Common Issues

#### "SkippedNoId" Errors

**Cause**: GDC records missing required identifiers
**Solution**: Verify GDC dataset quality; check field mapping

#### Graph API 429 Throttling

**Cause**: Too many concurrent requests
**Solution**: Reduce `Gdc:Parallelism` setting; monitor retry logs

#### Manifest Creation Failures

**Cause**: Insufficient disk space or permissions
**Solution**: Check output directory permissions and available space

#### Worker Not Detecting Files

**Cause**: File watcher configuration issues
**Solution**: Verify `Gdc:Input:Nas:Root` path exists and is accessible

### Log Analysis

Key log patterns to monitor:

```bash
# Success patterns
grep "BinaryFetched" logs/collection/collection-*.log
grep "ManifestCreated" logs/audit/worker-audit-*.log

# Error patterns
grep "Failed\|Error" logs/ediscovery-worker-*.log
grep "SkippedNoId" logs/collection/collection-*.log

# Performance patterns
grep "PERFORMANCE:" logs/collection/collection-*.log
```

### Support Escalation

For issues requiring escalation, collect:

1. Worker service logs
2. Sample GDC dataset (anonymized)
3. Configuration (`appsettings.json`)
4. Graph API error responses
5. Manifest files (if partially created)

## Future Enhancements

### Planned Features

- **Teams Chat Files**: Integration with Graph Export API
- **Modern Attachments**: Follow pointer resolution
- **Delta Processing**: Incremental GDC updates
- **Cloud Storage**: Direct Azure Blob/ADLS output

### Performance Improvements

- **Adaptive Parallelism**: Dynamic concurrency adjustment
- **Intelligent Retry**: ML-based retry strategy
- **Compression**: On-the-fly file compression
- **Deduplication**: Cross-custodian duplicate detection

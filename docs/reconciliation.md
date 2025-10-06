# Reconciliation & Validation System

## Overview

The Reconciliation & Validation system provides comprehensive validation between source manifests (what should have been collected) and collected manifests (what was actually collected). This system implements configurable tolerances, detailed discrepancy reporting, and pass/fail gates to ensure collection integrity.

## Architecture

The reconciliation system consists of several key components:

- **Reconciler Service**: Core reconciliation logic and manifest processing
- **ReconcileWorker**: Background service for automated reconciliation tasks
- **ReconcileOptions**: Configuration for tolerances and validation rules
- **ReconciliationModels**: Data models for manifest items, results, and discrepancies
- **API Endpoints**: REST API for triggering reconciliation operations
- **CLI Support**: Command-line interface for manual reconciliation tasks

## Configuration

Configure the reconciliation system in `appsettings.json`:

```json
{
  "Reconcile": {
    "SourceManifestPath": "./manifests/source",
    "CollectedManifestPath": "./manifests/collected",
    "ReportsPath": "./manifests/reports",
    "SizeTolerancePct": 0.1,
    "ExtraTolerancePct": 0.05,
    "RequireHashMatch": false,
    "SoftFail": false,
    "DryRun": false,
    "NormalizePaths": true,
    "IncludeFolders": false
  }
}
```

### Configuration Options

| Option                  | Type   | Default                 | Description                                             |
| ----------------------- | ------ | ----------------------- | ------------------------------------------------------- |
| `SourceManifestPath`    | string | "./manifests/source"    | Directory containing source manifests                   |
| `CollectedManifestPath` | string | "./manifests/collected" | Directory containing collected manifests                |
| `ReportsPath`           | string | "./manifests/reports"   | Directory for reconciliation reports                    |
| `SizeTolerancePct`      | double | 0.1                     | Allowed percentage difference in total size (0.1 = 10%) |
| `ExtraTolerancePct`     | double | 0.05                    | Allowed percentage of extra items (0.05 = 5%)           |
| `RequireHashMatch`      | bool   | false                   | Whether hash mismatches cause validation failure        |
| `SoftFail`              | bool   | false                   | Log discrepancies without failing validation            |
| `DryRun`                | bool   | false                   | Preview mode - don't save reports or update status      |
| `NormalizePaths`        | bool   | true                    | Normalize file paths for comparison                     |
| `IncludeFolders`        | bool   | false                   | Include folder items in reconciliation                  |

## Supported Manifest Formats

### CSV Manifest Format

The system supports CSV manifests with the following columns:

```csv
Path,Size,LastModified,DriveId,Hash,Kind
/Documents/file1.docx,15234,2024-01-15T10:30:00Z,b!abc123,sha256:def456,file
/Documents/subfolder/file2.pdf,45678,2024-01-16T14:20:00Z,b!abc123,sha256:789abc,file
```

**Required Columns:**

- `Path`: File or folder path
- `Size`: File size in bytes
- `LastModified`: ISO 8601 timestamp
- `DriveId`: Microsoft Graph drive identifier

**Optional Columns:**

- `Hash`: SHA-256 hash of file content
- `Kind`: Item type ('file' or 'folder')

### JSON Manifest Format

The system also supports line-delimited JSON (JSONL) format:

```json
{"Path":"/Documents/file1.docx","Size":15234,"LastModified":"2024-01-15T10:30:00Z","DriveId":"b!abc123","Hash":"sha256:def456","Kind":"file"}
{"Path":"/Documents/subfolder/file2.pdf","Size":45678,"LastModified":"2024-01-16T14:20:00Z","DriveId":"b!abc123","Hash":"sha256:789abc","Kind":"file"}
```

## Reconciliation Logic

### Matching Rules

Items are matched between source and collected manifests using the following logic:

1. **Primary Key Generation**: `DriveId|Path|Size|LastModified`
2. **Path Normalization**: Convert to forward slashes, trim leading/trailing slashes, lowercase
3. **Folder Handling**: Optionally include or exclude folder items based on configuration
4. **Hash Validation**: Compare SHA-256 hashes when available (optional enforcement)

### Validation Gates

The system applies several validation gates with configurable tolerances:

#### 1. Cardinality Gate

- **Rule**: No missing items allowed
- **Tolerance**: None (always enforced)
- **Failure**: Any item in source manifest but not in collected manifest

#### 2. Extra Items Gate

- **Rule**: Limited extra items allowed
- **Tolerance**: `ExtraTolerancePct` (percentage of source item count)
- **Calculation**: `(ExtraCount / SourceCount) * 100 <= ExtraTolerancePct`
- **Failure**: Too many extra items collected

#### 3. Size Validation Gate

- **Rule**: Total size difference within tolerance
- **Tolerance**: `SizeTolerancePct` (percentage of source total size)
- **Calculation**: `|CollectedSize - SourceSize| / SourceSize * 100 <= SizeTolerancePct`
- **Failure**: Size difference exceeds tolerance

#### 4. Hash Validation Gate (Optional)

- **Rule**: Hash mismatches not allowed when enforced
- **Tolerance**: None when `RequireHashMatch = true`
- **Failure**: Any hash mismatch when enforcement enabled

### Overall Pass/Fail Logic

```csharp
OverallPassed = CardinalityPassed && ExtrasPassed && SizePassed && HashPassed
```

## API Usage

### Trigger Reconciliation

Trigger reconciliation for a completed collection job:

```http
POST /api/jobs/{jobId}/reconcile
Content-Type: application/json

{
  "custodianFilter": "john.doe@company.com",
  "sourceManifestPath": "./manifests/source/job-123-source.csv",
  "collectedManifestPath": "./manifests/collected/job-123-collected.csv",
  "dryRun": false
}
```

**Response:**

```json
{
  "message": "Reconciliation request accepted",
  "jobId": 123,
  "request": {
    "custodianFilter": "john.doe@company.com",
    "sourceManifestPath": "./manifests/source/job-123-source.csv",
    "collectedManifestPath": "./manifests/collected/job-123-collected.csv",
    "dryRun": false
  }
}
```

## CLI Usage

Run reconciliation from the command line:

```bash
# Basic reconciliation
./HybridGraphCollectorWorker --reconcile john.doe@company.com 123 ./source.csv ./collected.csv

# Dry run mode (preview only)
./HybridGraphCollectorWorker --reconcile john.doe@company.com 123 ./source.csv ./collected.csv --dry-run
```

**CLI Arguments:**

1. `--reconcile`: CLI mode flag
2. `custodian`: Custodian email address
3. `jobId`: Collection job identifier
4. `sourcePath`: Path to source manifest file
5. `collectedPath`: Path to collected manifest file
6. `--dry-run`: Optional flag for preview mode

**Exit Codes:**

- `0`: Reconciliation passed all validation gates
- `1`: Reconciliation failed or error occurred

## Report Generation

### Reconciliation Report Structure

The system generates detailed reconciliation reports in both CSV and JSON formats:

```
./manifests/reports/
├── reconciliation-{jobId}-{timestamp}.csv
├── reconciliation-{jobId}-{timestamp}.json
└── reconciliation-{jobId}-{timestamp}-summary.txt
```

### CSV Report Format

```csv
Category,Path,Size,LastModified,DriveId,Hash,Issue
Missed,/Documents/missing.docx,12345,2024-01-15T10:30:00Z,b!abc123,sha256:def456,Item in source but not collected
Extra,/Documents/unexpected.pdf,54321,2024-01-16T14:20:00Z,b!abc123,sha256:789abc,Item collected but not in source
HashMismatch,/Documents/modified.docx,12345,2024-01-15T10:30:00Z,b!abc123,sha256:different,Content hash differs between source and collected
```

### JSON Report Format

```json
{
  "jobId": "123",
  "custodian": "john.doe@company.com",
  "processedUtc": "2024-01-20T15:30:00Z",
  "sourceCount": 1000,
  "collectedCount": 995,
  "missedCount": 5,
  "extraCount": 0,
  "hashMismatchCount": 2,
  "sourceTotalBytes": 1048576000,
  "collectedTotalBytes": 1045430400,
  "sizeDeltaBytes": -3145600,
  "overallPassed": false,
  "cardinalityPassed": false,
  "extrasPassed": true,
  "sizePassed": true,
  "hashPassed": true,
  "missed": [
    {
      "path": "/Documents/missing.docx",
      "size": 12345,
      "lastModified": "2024-01-15T10:30:00Z",
      "driveId": "b!abc123",
      "hash": "sha256:def456",
      "issue": "Item in source but not collected"
    }
  ],
  "extras": [],
  "hashMismatches": [
    {
      "path": "/Documents/modified.docx",
      "size": 12345,
      "lastModified": "2024-01-15T10:30:00Z",
      "driveId": "b!abc123",
      "hash": "sha256:different",
      "issue": "Content hash differs between source and collected"
    }
  ]
}
```

## Compliance Logging

The reconciliation system integrates with the compliance logging framework to provide audit trails:

### Audit Events

| Event                     | Description                         | Context                                     |
| ------------------------- | ----------------------------------- | ------------------------------------------- |
| `ReconciliationStarted`   | Reconciliation process initiated    | JobId, Custodian, SourcePath, CollectedPath |
| `ManifestLoaded`          | Source or collected manifest loaded | ManifestType, ItemCount, TotalBytes         |
| `ReconciliationCompleted` | Reconciliation process finished     | Result summary, PassFail status             |
| `ReportGenerated`         | Reconciliation report saved         | ReportPath, Format                          |
| `ValidationFailed`        | Validation gate failure             | Gate, Expected, Actual, Tolerance           |

### Sample Audit Log

```json
{
  "timestamp": "2024-01-20T15:30:00Z",
  "level": "Information",
  "event": "ReconciliationStarted",
  "jobId": "123",
  "custodian": "john.doe@company.com",
  "correlationId": "rec_a1b2c3d4",
  "context": {
    "sourceManifest": "./manifests/source/job-123-source.csv",
    "collectedManifest": "./manifests/collected/job-123-collected.csv",
    "dryRun": false
  }
}
```

## Error Handling

### Common Error Scenarios

1. **Missing Manifest Files**

   - **Error**: Source or collected manifest file not found
   - **Resolution**: Verify file paths and ensure manifests exist

2. **Invalid Manifest Format**

   - **Error**: Unable to parse CSV/JSON manifest format
   - **Resolution**: Verify manifest format matches expected schema

3. **Empty Manifests**

   - **Error**: Source or collected manifest contains no items
   - **Resolution**: Check collection process and manifest generation

4. **Path Normalization Issues**
   - **Error**: Path comparison failures due to encoding or format differences
   - **Resolution**: Enable path normalization in configuration

### Error Recovery

The reconciliation system implements robust error handling:

- **Partial Failures**: Continue processing when individual items fail validation
- **Format Detection**: Auto-detect CSV vs JSON manifest formats
- **Graceful Degradation**: Generate partial reports when possible
- **Detailed Error Logging**: Comprehensive error context for troubleshooting

## Performance Considerations

### Large Manifest Handling

- **Streaming Processing**: Large manifests processed in chunks to manage memory
- **Parallel Processing**: Multi-threaded comparison operations for performance
- **Progress Reporting**: Real-time progress updates for long-running operations

### Optimization Recommendations

1. **Path Indexing**: Pre-index normalized paths for faster lookups
2. **Hash Caching**: Cache computed hashes to avoid recalculation
3. **Incremental Processing**: Process only new/changed items when possible
4. **Batch Reporting**: Generate reports in batches for large datasets

## Integration Examples

### Worker Service Integration

```csharp
// Background reconciliation triggered by job completion
public class ReconcileWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Check for completed jobs needing reconciliation
            var pendingJobs = await GetPendingReconciliationJobs();

            foreach (var job in pendingJobs)
            {
                var result = await _reconciler.ReconcileAsync(
                    job.Custodian,
                    job.Id.ToString(),
                    job.SourceManifestPath,
                    job.CollectedManifestPath,
                    stoppingToken);

                await UpdateJobWithReconciliationResult(job.Id, result);
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

### API Controller Integration

```csharp
[HttpPost("{id}/reconcile")]
public async Task<ActionResult> ReconcileJob(int id, [FromBody] ReconcileRequest request)
{
    var job = await _context.CollectionJobs.FindAsync(id);
    if (job?.Status != CollectionJobStatus.Completed)
    {
        return BadRequest("Job must be completed before reconciliation");
    }

    // Queue reconciliation task
    var result = await _reconciler.ReconcileAsync(
        request.CustodianFilter ?? job.CustodianEmail,
        id.ToString(),
        request.SourceManifestPath ?? GetDefaultSourcePath(id),
        request.CollectedManifestPath ?? GetDefaultCollectedPath(id));

    return Ok(new { Passed = result.OverallPassed, Report = result.ReportPath });
}
```

## Troubleshooting Guide

### Common Issues

**Q: Reconciliation always fails with "no items found"**
A: Check manifest file paths and formats. Ensure CSV headers match expected schema.

**Q: Size validation fails despite identical file counts**
A: Review size tolerance settings. Large files may have slight size differences due to metadata.

**Q: Hash mismatches for identical files**
A: Verify hash algorithms match between source and collection. Check for encoding differences.

**Q: Path comparison failures**
A: Enable path normalization and verify path encoding consistency.

### Debug Logging

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "HybridGraphCollectorWorker.Services.Reconciler": "Debug"
    }
  }
}
```

This documentation provides comprehensive guidance for implementing, configuring, and using the reconciliation system effectively within the hybrid eDiscovery collection platform.

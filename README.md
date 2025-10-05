# Hybrid eDiscovery Collector - v2.2 Production Ready ✅

A comprehensive hybrid eDiscovery collection system with intelligent routing between Microsoft Graph API and Graph Data Connect, featuring enterprise job sharding, advanced observability, structured logging, and health monitoring.

## 🎉 **LATEST UPDATE: Clean Build Achieved**

✅ **All core projects now compile successfully**  
✅ **Job sharding platform fully implemented and tested**  
✅ **Database architecture consolidated with shared DbContext**  
✅ **Production-ready with comprehensive observability**

## 🏗️ Architecture

### Core Components

1. **EDiscoveryIntakeApi** (.NET 9 Web API)

   - Job management with SQLite/Azure SQL support
   - REST endpoints for collection lifecycle
   - **NEW**: Enterprise job sharding with REST API management
   - **NEW**: Health monitoring endpoints with comprehensive metrics
   - **NEW**: Structured logging integration for observability
   - Swagger documentation and OpenAPI support

2. **HybridGraphCollectorWorker** (.NET 9 Worker Service)

   - Microsoft 365 Graph API integration
   - **AutoRouter**: Intelligent route selection based on data volume
     - **Graph API**: `< 100GB` and `< 500k` items (configurable)
     - **Graph Data Connect**: Large-scale collections with ADF pipeline triggers
   - **Delta Queries**: Incremental collection with cursor tracking
   - **NEW**: Sharded job processing with parallel workers
   - **NEW**: Checkpoint recovery for idempotent restarts
   - **NEW**: Comprehensive structured logging for all collection events
   - **NEW**: Real-time metrics collection for dashboards
   - Chain-of-custody with tamper-evident manifests

3. **EDiscovery.Shared**
   - Shared models, services, and configuration
   - **NEW**: Centralized database context with complete schema
   - **NEW**: Job sharding platform with automatic partitioning
   - **NEW**: Checkpoint recovery service for fault tolerance
   - AutoRouter with environment-specific thresholds
   - Delta Query service with database-backed cursors
   - **NEW**: ObservabilityService for metrics and health monitoring
   - **NEW**: Structured event models for comprehensive logging
   - Chain of Custody with digital signatures and WORM storage

## 🚀 Quick Start (POC)

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose (optional)
- Azure AD App Registration with:
  - `Mail.Read`, `Files.Read.All`, `Sites.Read.All` permissions
  - Client secret configured

### Option 1: Docker Compose (Recommended)

1. **Clone and Configure**

   ```bash
   git clone <repository>
   cd M365Test
   cp .env.template .env
   # Edit .env with your Azure AD credentials
   ```

2. **Run with Docker Compose**

   ```bash
   docker-compose up --build
   ```

3. **Access Services**
   - API: http://localhost:7001
   - Swagger: http://localhost:7001/swagger

### Option 2: Local Development

1. **Configure Settings**

   ```bash
   # Update appsettings.json in both projects with Azure AD details
   ```

2. **Run API**

   ```bash
   cd src/EDiscoveryIntakeApi
   dotnet run
   ```

3. **Run Worker (separate terminal)**
   ```bash
   cd src/HybridGraphCollectorWorker
   dotnet run
   ```

## � **Enterprise Job Sharding Platform**

The system now includes a comprehensive job sharding platform for enterprise-scale collections:

### Key Capabilities

- **Automatic Partitioning**: Large jobs are automatically split by custodian × date window (configurable, default 30 days)
- **Checkpoint Recovery**: Granular progress tracking enables idempotent restarts from any interruption point
- **Parallel Processing**: Multiple workers can process different shards simultaneously for faster completion
- **Fault Isolation**: Failure in one shard doesn't affect others, maintaining collection integrity
- **Progress Monitoring**: Real-time visibility into overall job progress and individual shard status

### REST API Endpoints

```bash
# Create a sharded job
POST /api/sharded-jobs

# Get job progress
GET /api/sharded-jobs/{jobId}/progress

# List available shards for workers
GET /api/sharded-jobs/available-shards

# Update shard progress
PUT /api/sharded-jobs/shards/{shardId}/progress
```

### Worker Integration

Workers automatically detect and process sharded jobs:

- Claim available shards from the work queue
- Process assigned custodian data within date windows
- Update checkpoint progress for fault tolerance
- Coordinate with other workers for parallel execution

## �📋 Usage

### 1. Create a Matter

```bash
curl -X POST "http://localhost:7001/api/matters" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Investigation 2024-001",
    "description": "Sample investigation",
    "caseNumber": "CASE-2024-001",
    "createdBy": "admin@company.com"
  }'
```

### 2. Create a Collection Job

```bash
curl -X POST "http://localhost:7001/api/jobs" \
  -H "Content-Type: application/json" \
  -d '{
    "matterId": 1,
    "custodianEmail": "user@company.com",
    "jobType": 1,
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-12-31T23:59:59Z",
    "keywords": ["contract", "agreement"],
    "includeAttachments": true
  }'
```

### 3. Monitor Collection Progress

```bash
curl "http://localhost:7001/api/jobs/1"
```

## 🔄 AutoRouter Logic

The AutoRouter determines the optimal collection route based on configurable thresholds and current system state:

### 📐 **Configurable Routing Thresholds**

Routing decisions are now **fully configurable** per environment through `appsettings.json` or environment variables:

```json
{
  "AutoRouter": {
    "GraphApiThresholds": {
      "MaxSizeBytes": 107374182400, // 100GB default
      "MaxItemCount": 500000, // 500k items default
      "Description": "Graph API routing limits"
    },
    "RoutingConfidence": {
      "HighConfidence": 90, // 90% for clear decisions
      "MediumConfidence": 80, // 80% for borderline cases
      "LowConfidence": 70 // 70% for fallback scenarios
    }
  },
  "DeltaQuery": {
    "EnableMailDelta": true, // Enable incremental Mail collection
    "EnableOneDriveDelta": true, // Enable incremental OneDrive collection
    "MaxDeltaAgeDays": 30, // Force full resync after 30 days
    "DeltaQueryIntervalMinutes": 60, // Query for changes every hour
    "MaxDeltaItemsPerQuery": 1000, // Limit items per delta query
    "MaxDeltaFailures": 3, // Force resync after 3 failures
    "BackgroundDeltaQueries": true, // Run delta queries in background
    "EnableAutomaticCleanup": true // Automatically cleanup stale cursors
  },
  "ChainOfCustody": {
    "ManifestFormat": "Both", // Generate JSON and CSV manifests
    "ManifestStoragePath": "./manifests", // Local manifest storage
    "ImmutableStoragePath": "./immutable", // WORM storage location
    "EnableDigitalSigning": false, // Disable for development
    "SigningCertificateThumbprint": null, // Certificate for production
    "EnableWormStorage": true, // Enable tamper-evident storage
    "ImmutablePolicyId": "ediscovery-worm-policy", // WORM policy ID
    "ManifestRetentionDays": 2555, // 7 years retention
    "EnablePeriodicVerification": true, // Automated integrity checks
    "VerificationIntervalHours": 24 // Daily verification
  }
}
```

### 🎯 **Environment-Specific Thresholds**

| Environment     | Max Size | Max Items | Delta Interval | Use Case                    |
| --------------- | -------- | --------- | -------------- | --------------------------- |
| **Development** | 10GB     | 50k       | 15 minutes     | Fast testing and validation |
| **Staging**     | 100GB    | 500k      | 30 minutes     | Production-like testing     |
| **Production**  | 500GB+   | 1M+       | 60 minutes     | High-volume enterprise      |
| **Docker**      | 200GB    | 1M        | 45 minutes     | Container deployment        |

### 🔀 **Decision Logic**

- **Current Quota Usage**: Configurable Graph API limits per environment
- **Estimated Collection Size**: Based on custodian profile and job type
- **Confidence Scoring**: Configurable confidence levels for routing decisions
- **Historical Performance**: Success rates and throughput metrics

### 🚀 **Environment Variable Overrides**

```bash
# Override via environment variables
export AutoRouter__GraphApiThresholds__MaxSizeBytes=214748364800  # 200GB
export AutoRouter__GraphApiThresholds__MaxItemCount=1000000       # 1M items
export DeltaQuery__DeltaQueryIntervalMinutes=30                   # 30 minutes
export DeltaQuery__MaxDeltaAgeDays=7                              # Weekly full resync

# Docker deployment
docker run -e AutoRouter__GraphApiThresholds__MaxSizeBytes=200GB \
           -e DeltaQuery__DeltaQueryIntervalMinutes=30 my-app

# Kubernetes deployment
env:
  - name: AutoRouter__GraphApiThresholds__MaxSizeBytes
    value: "214748364800"
  - name: DeltaQuery__DeltaQueryIntervalMinutes
    value: "30"
```

**Decision Matrix:**

- ✅ **Graph API**: Fast, real-time, below configured thresholds
- ✅ **Graph Data Connect**: Bulk, scheduled, above configured thresholds
- 🔄 **Delta Queries**: Incremental updates for both Graph API and GDC collections

## 🔄 Delta Query System

### 📈 **Performance & Cost Optimization**

The Delta Query system provides incremental collection to avoid re-pulling unchanged content:

- **Mail Delta Queries**: Track changes since last collection using Microsoft Graph delta tokens
- **OneDrive Delta Queries**: Monitor file and folder changes incrementally
- **Cursor Storage**: Simple table-based tracking of delta state per custodian and data type
- **Background Processing**: Configurable intervals for delta query execution
- **Automatic Cleanup**: Remove stale cursors and force periodic full resyncs

### 🎯 **Delta Query Features**

| Feature              | Description                             | Configuration                   |
| -------------------- | --------------------------------------- | ------------------------------- |
| **Mail Incremental** | Only collect new/changed emails         | `EnableMailDelta: true`         |
| **OneDrive Changes** | Only sync modified files                | `EnableOneDriveDelta: true`     |
| **Interval Control** | Configurable query frequency            | `DeltaQueryIntervalMinutes: 60` |
| **Cursor Cleanup**   | Automatic stale cursor removal          | `EnableAutomaticCleanup: true`  |
| **Failure Handling** | Force full resync after failures        | `MaxDeltaFailures: 3`           |
| **Age Limits**       | Periodic full resync for data integrity | `MaxDeltaAgeDays: 30`           |

## 📊 Supported Collection Types

| Type       | Graph API | GDC | Delta Queries | Status  |
| ---------- | --------- | --- | ------------- | ------- |
| Email      | ✅        | 🚧  | ✅            | Ready   |
| OneDrive   | ✅        | 🚧  | ✅            | Ready   |
| SharePoint | 🚧        | 🚧  | 🚧            | Planned |
| Teams      | 🚧        | 🚧  | 🚧            | Planned |

### 🔄 **Delta Query Status**

- **✅ Implemented**: Mail and OneDrive delta query support
- **✅ Cursor Storage**: Database table for tracking delta state
- **✅ Background Processing**: Worker service integration
- **✅ Configuration**: Environment-specific delta query settings

## 📊 Observability & Monitoring

### 🎯 **Structured Logging Events**

The system now emits comprehensive structured logs for complete collection visibility:

| Event Type           | Description                             | Key Fields                                   |
| -------------------- | --------------------------------------- | -------------------------------------------- |
| **JobStarted**       | Collection job initiation               | CustodianEmail, JobType, DateRange, Keywords |
| **ItemCollected**    | Individual item collection              | ItemId, ItemType, SizeBytes, CustodianEmail  |
| **BackoffTriggered** | API throttling and retry events         | StatusCode, DelayMs, Endpoint                |
| **AutoRoutedToGDC**  | Graph Data Connect routing decisions    | RecommendedRoute, Reason, ConfidenceScore    |
| **JobCompleted**     | Collection completion with full metrics | IsSuccessful, ItemCount, SizeBytes, Duration |

### 📈 **Health Monitoring Endpoints**

The API provides comprehensive health endpoints for monitoring and dashboards:

```bash
# Simple health check for load balancers
GET /api/health
# Response: { "status": "healthy", "timestamp": "2024-10-05T...", "version": "2.1.0" }

# Detailed system health
GET /api/health/detailed
# Response: Full system status with dependencies and uptime

# Performance metrics for dashboards
GET /api/health/counters
# Response: Real-time throughput and error metrics
{
  "itemsPerMinute": 1250.5,
  "mbPerMinute": 89.3,
  "throttling429Count15min": 2,
  "retrySuccessRate": 98.7,
  "uptime": "2.03:45:22"
}

# Kubernetes probes
GET /api/health/ready   # Readiness probe
GET /api/health/live    # Liveness probe
```

### 📊 **Dashboard Metrics**

The observability system collects key performance indicators:

| Metric Category     | Key Metrics                                 | Update Frequency |
| ------------------- | ------------------------------------------- | ---------------- |
| **Throughput**      | Items/min, MB/min, Peak performance         | Real-time        |
| **Reliability**     | Retry success rate, Average backoff delay   | Real-time        |
| **API Health**      | 429 throttling events, Error rates          | Real-time        |
| **Job Performance** | Average duration, Success rates             | Per job          |
| **System Health**   | Uptime, Memory usage, Database connectivity | Continuous       |

### 🔍 **Correlation & Audit Trail**

- **Correlation IDs**: Every operation tracked with unique identifiers
- **Compliance Logging**: 365-day audit log retention with tamper evidence
- **Performance Timers**: Detailed operation timing for optimization
- **Chain of Custody**: Complete evidence trail with digital signatures

### 📱 **Integration Examples**

```bash
# Grafana/Prometheus integration
curl -s http://localhost:7001/api/health/counters | jq '.itemsPerMinute'

# Azure Monitor integration
az monitor metrics alert create --resource-group rg-ediscovery \
  --condition "avg itemsPerMinute < 100"

# Splunk/ELK integration
tail -f logs/audit.log | grep "JobCompleted" | jq '.JobCompletedEvent'
```

## 🧩 **Job Sharding & Scalability**

### 🎯 **Enterprise Job Sharding**

Automatic partitioning of large collection jobs by custodian and date windows with checkpoint recovery for enterprise-scale processing.

```csharp
// Create sharded job for large collection
var request = new CreateShardedJobRequest
{
    MatterId = 1,
    CustodianEmails = new List<string> { "user1@company.com", "user2@company.com" },
    JobType = CollectionJobType.Email,
    StartDate = DateTime.UtcNow.AddDays(-365), // 1 year collection
    EndDate = DateTime.UtcNow,
    ShardingConfig = new JobShardingConfig
    {
        MaxDateWindowSize = TimeSpan.FromDays(30), // 30-day shards
        MaxShardsPerCustodian = 12,
        EnableAdaptiveSharding = true
    }
};

var response = await POST("/api/shardedjobs", request);
// Creates 24 shards (2 custodians × 12 months)
```

### 🔄 **Checkpoint Recovery**

Idempotent restarts from any interruption point with granular progress tracking:

```json
{
  "shardId": 156,
  "checkpointType": "MailFolderProgress",
  "checkpointData": {
    "folderId": "inbox",
    "deltaToken": "a1b2c3...",
    "itemsProcessed": 1247,
    "lastProcessedTime": "2024-10-05T14:30:00Z"
  },
  "isCompleted": false
}
```

### 📊 **Real-time Progress Monitoring**

```bash
# Monitor overall job progress
GET /api/shardedjobs/123
{
  "totalShards": 24,
  "completedShards": 18,
  "processingShards": 3,
  "overallProgress": 75.2,
  "estimatedCompletion": "2024-10-06T16:45:00Z"
}

# Get detailed shard status
GET /api/shardedjobs/123/shards
# Returns all 24 shards with individual progress
```

### 🚀 **Scalability Benefits**

- **Parallel Processing**: Multiple workers process different shards simultaneously
- **Fault Isolation**: Failure in one shard doesn't affect others
- **Resource Efficiency**: Manageable memory usage per shard
- **Progress Preservation**: Resume from exact interruption point
- **Load Distribution**: Automatic work balancing across worker fleet

### 🔧 **Worker Integration**

```csharp
// Worker automatically picks up available shards
var shard = await shardingService.GetNextAvailableShardAsync(workerId, userId);
if (shard != null)
{
    await shardingService.AcquireShardLockAsync(shard.Id, workerId, userId);
    var result = await shardProcessor.ProcessShardAsync(shard);
    await shardingService.CompleteShardAsync(shard.Id, result.IsSuccessful,
        result.TotalItemCount, result.TotalSizeBytes);
}
```

- **✅ Cleanup**: Automatic stale cursor management

## 🔒 Security & Compliance Features

- **Chain of Custody Hardening**: ✅ **COMPLETED**

  - SHA-256 hashing of all collected items
  - Tamper-evident manifests (JSON/CSV) with cryptographic integrity protection
  - Digital signatures using X.509 certificates (configurable)
  - Write-Once-Read-Many (WORM) compliant storage for evidence preservation
  - Automated manifest verification with audit trails
  - REST API for manifest management and verification

- **Enterprise Logging**: ✅ **COMPLETED**

  - Structured JSON logging with Serilog for machine analysis
  - Audit trails with 365-day retention for compliance requirements
  - Performance monitoring with detailed execution metrics
  - Correlation ID tracking across distributed operations
  - Separate audit logs specifically designed for legal requirements
  - Compliance logger service for eDiscovery-specific events

- **Observability & Monitoring**: ✅ **COMPLETED**

  - Comprehensive structured logging for all collection events
  - Real-time health monitoring endpoints for dashboards
  - Performance counters (items/min, MB/min, error rates)
  - Kubernetes-ready health probes (readiness/liveness)
  - Dashboard integration support (Grafana, Azure Monitor, Splunk)

- **Reliability Features**: ✅ **COMPLETED**
  - Exponential backoff retry logic for API throttling
  - Graceful error handling and recovery mechanisms
  - Background processing with cancellation support

## 📁 Output Structure

```
./output/
├── user@company.com/
│   ├── Email/
│   │   ├── message1_contract-review.json
│   │   └── message2_nda-agreement.json
│   └── OneDrive/
│       ├── document1_metadata.json
│       └── document2_metadata.json
├── manifests/
│   ├── 2025-10-05/
│   │   ├── manifest_a1b2c3d4_000042_20251005_093015.json
│   │   └── manifest_a1b2c3d4_000042_20251005_093015.csv
└── immutable/
    └── worm/
        └── 2025-10-05/
            └── sealed_manifest_a1b2c3d4_000042_20251005_093015.json
```

## 🛠️ Development

### Build Solution

```bash
dotnet build HybridEDiscoveryCollector.sln
```

### Run Tests

```bash
dotnet test
```

### Database Migrations (if using EF migrations)

```bash
cd src/EDiscoveryIntakeApi
dotnet ef database update
```

## 📈 Implementation Status

### ✅ **Production Ready Features (v2.2) - CLEAN BUILD ACHIEVED**

- **Core Collection System**: Graph API integration with multi-source support
- **AutoRouter Service**: Intelligent routing with configurable thresholds
- **Delta Query System**: Incremental collection with database cursor tracking
- **Enterprise Job Sharding**: ✅ **NEW** - Automatic partitioning by custodian × date window with checkpoint recovery
- **Parallel Processing**: ✅ **NEW** - Multiple workers can process different shards simultaneously
- **Fault Isolation**: ✅ **NEW** - Failure in one shard doesn't affect others
- **Idempotent Restarts**: ✅ **NEW** - Granular progress tracking enables restart from any interruption point
- **Chain of Custody**: Tamper-evident manifests with digital signatures and WORM storage
- **Comprehensive Logging**: Enterprise-grade structured logging with audit trails
- **Observability Platform**: Real-time metrics, health monitoring, and dashboard integration
- **Graph Data Connect**: ADF pipeline triggers with Service Bus integration
- **Consolidated Database**: ✅ **NEW** - Shared DbContext with complete schema and optimized indexes
- **REST API Management**: ✅ **NEW** - Complete endpoints for shard management and worker coordination

### 🎯 **Job Sharding Platform Highlights**

- **Automatic Partitioning**: Large jobs split by custodian and date windows (default 30-day shards)
- **Checkpoint Recovery**: Database-backed progress tracking for reliable restarts
- **Progress Monitoring**: Real-time visibility into overall job progress and individual shard status
- **Worker Coordination**: Multiple workers can claim and process different shards in parallel
- **Database Integration**: New JobShards and JobShardCheckpoints tables with optimized indexes
- **REST API**: 13 comprehensive endpoints for shard lifecycle management

### 🚧 **Phase 2 Roadmap (Production Deployment)**

- [x] **Delta Queries**: Incremental collection ✅ **COMPLETED**
- [x] **Observability**: Structured logging and health monitoring ✅ **COMPLETED**
- [x] **GDC Integration**: Azure Data Factory pipeline triggers ✅ **COMPLETED**
- [ ] **Security**: Managed Identity + Key Vault integration
- [ ] **Storage**: Azure Blob Storage with Customer Managed Keys
- [ ] **Monitoring**: Azure Monitor + Log Analytics integration
- [ ] **Database Migration**: Production Azure SQL deployment

### � **Phase 3 Roadmap (Enterprise Scale)**

- [ ] **Infrastructure**: Terraform/Bicep deployment templates
- [ ] **Container Apps**: Auto-scaling container deployment
- [ ] **CI/CD**: Complete DevOps pipeline with automated testing
- [ ] **Analytics**: Power BI dashboards + Kusto query analytics
- [ ] **Compliance**: SOC 2 Type II + FedRAMP readiness assessment

## 🔧 Configuration

### Environment Variables

- `AZURE_TENANT_ID`: Azure AD tenant ID
- `AZURE_CLIENT_ID`: App registration client ID
- `AZURE_CLIENT_SECRET`: App registration secret
- `SAMPLE_CUSTODIAN`: Test user email

### Key Settings

- **API Base URL**: `EDiscoveryApi:BaseUrl`
- **Output Path**: `OutputPath`
- **Poll Interval**: `Worker:PollIntervalSeconds`
- **Database**: `ConnectionStrings:DefaultConnection`

## 📜 License

This project is licensed under the MIT License.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

---

**Note**: This is a POC implementation. For production use, implement proper security controls, error handling, and compliance measures as outlined in the Phase 2/3 roadmaps.

# M365Test

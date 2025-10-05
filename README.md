# Hybrid eDiscovery Collector - POC

A hybrid eDiscovery collection system that intelligently routes between Microsoft Graph API and Graph Data Connect based on collection size and complexity.

## 🏗️ Architecture

### Components

1. **EDiscoveryIntakeApi** (.NET 8 Web API)

   - Tracks matters, collection jobs, and collected items
   - Provides REST endpoints for job management
   - Uses SQLite for POC (Azure SQL for production)
   - Includes Swagger documentation

2. **HybridGraphCollectorWorker** (.NET 8 Worker Service)

   - Connects to Microsoft 365 via Graph API
   - **AutoRouter** determines optimal collection route:
     - **Graph API**: `< 100GB` and `< 500k` items
     - **Graph Data Connect**: Above thresholds
   - **Delta Queries**: Incremental collection to avoid re-pulling unchanged content
   - Features:
     - Retry/backoff for 429s and 5xx errors
     - Chain-of-custody logging with SHA-256 hashes
     - NAS or Azure Blob outputs
     - Integration with Intake API
     - Delta cursor tracking for Mail and OneDrive

3. **EDiscovery.Shared**
   - Common models and services
   - AutoRouter service with configurable thresholds
   - Delta Query service for incremental collection
   - Data entities (Matter, CollectionJob, CollectedItem, JobLog, DeltaCursor)
   - Comprehensive logging and compliance services

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

## 📋 Usage

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
- **✅ Cleanup**: Automatic stale cursor management

## 🔒 Security Features

- **Chain of Custody**: SHA-256 hashing of all collected items
- **Comprehensive Logging**: Enterprise-grade structured logging with Serilog
  - Audit trails with 365-day retention for compliance
  - Performance monitoring with execution metrics
  - Correlation ID tracking across distributed operations
  - Separate audit logs for legal requirements
  - JSON structured format for machine analysis
- **Retry Logic**: Exponential backoff for API throttling
- **Error Handling**: Graceful degradation and recovery

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
└── manifest.json
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

## 📈 Phase 2 Roadmap (Production)

- [x] **Delta Queries**: Incremental collection ✅ **COMPLETED**
- [ ] **Security**: Managed Identity + Key Vault
- [ ] **Storage**: Azure Blob Storage with CMK
- [ ] **GDC**: Azure Data Factory pipelines
- [ ] **Monitoring**: Azure Monitor + Log Analytics
- [ ] **Signed Manifests**: Immutable evidence packages
- [ ] **Database Migration**: Entity Framework migrations for DeltaCursors table

## 📈 Phase 3 Roadmap (Enterprise)

- [ ] **Infrastructure**: Terraform/Bicep deployment
- [ ] **Container Apps**: Scalable deployment
- [ ] **CI/CD**: Complete DevOps pipeline
- [ ] **Dashboards**: Power BI + Kusto analytics
- [ ] **Compliance**: SOC 2 + FedRAMP readiness

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

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
   - Features:
     - Retry/backoff for 429s and 5xx errors
     - Chain-of-custody logging with SHA-256 hashes
     - NAS or Azure Blob outputs
     - Integration with Intake API

3. **EDiscovery.Shared**
   - Common models and services
   - AutoRouter service
   - Data entities (Matter, CollectionJob, CollectedItem, JobLog)

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

The AutoRouter determines the optimal collection route based on:

- **Current Quota Usage**: Graph API limits (100GB, 500k items)
- **Estimated Collection Size**: Based on custodian and job type
- **Historical Performance**: Success rates and throughput

**Decision Matrix:**

- ✅ **Graph API**: Fast, real-time, < 100GB
- ✅ **Graph Data Connect**: Bulk, scheduled, > 100GB

## 📊 Supported Collection Types

| Type       | Graph API | GDC | Status  |
| ---------- | --------- | --- | ------- |
| Email      | ✅        | 🚧  | Ready   |
| OneDrive   | ✅        | 🚧  | Ready   |
| SharePoint | 🚧        | 🚧  | Planned |
| Teams      | 🚧        | 🚧  | Planned |

## 🔒 Security Features

- **Chain of Custody**: SHA-256 hashing of all collected items
- **Audit Logging**: Comprehensive job and event logging
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

- [ ] **Security**: Managed Identity + Key Vault
- [ ] **Storage**: Azure Blob Storage with CMK
- [ ] **GDC**: Azure Data Factory pipelines
- [ ] **Monitoring**: Azure Monitor + Log Analytics
- [ ] **Delta Queries**: Incremental collection
- [ ] **Signed Manifests**: Immutable evidence packages

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

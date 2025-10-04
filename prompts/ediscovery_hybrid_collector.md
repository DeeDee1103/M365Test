# Hybrid eDiscovery Collector – Copilot Agent Context

## Purpose

You are assisting in building a **hybrid eDiscovery collection system** for a large financial organization.  
The system replaces Microsoft Purview's collection limitations by using:

- **Microsoft Graph API** for small custodians and ad-hoc collections.
- **Microsoft Graph Data Connect (GDC)** for large custodians and bulk transfers.

The application must be **secure, scalable, compliant, and fully automatable**.

---

## Components

### 1. EDiscoveryIntakeApi (.NET 8 Web API)

- Tracks matters, jobs, and collected items.
- Provides endpoints to start and finish jobs, and record collected items.
- Persists data in SQLite (POC) or Azure SQL (production).

### 2. HybridGraphCollectorWorker (.NET 8 Worker)

- Connects to M365 via **Microsoft Graph API**.
- Includes an **AutoRouter** that determines route:
  - **Graph API** if `quota.used < 100GB` and mail items < 500k.
  - **Graph Data Connect** if above thresholds.
- Supports:
  - Retry/backoff for 429s and 5xx errors.
  - Chain-of-custody logging with SHA-256 hashes.
  - NAS or Azure Blob outputs.
  - Integration with the Intake API.

---

## Deployment Targets

- **Phase 1 (POC)**  
  Local or Docker Compose with client secret auth and NAS output.  
  ✅ Bundles:

  - `ediscovery-end-to-end.zip`
  - `ediscovery-poc-docker.zip`

- **Phase 2 (Production)**

  - Replace secrets with **Managed Identity + Key Vault**
  - Use **Azure Blob Storage (CMK)** + **Private Endpoints**
  - Add delta queries, ADF (GDC) trigger + poller, signed manifests
  - Push metrics to **Azure Monitor / Log Analytics**

- **Phase 3 (Shippable)**  
  Containerized deployment via **Azure Container Apps** or **AKS**, IaC with Terraform/Bicep, full CI/CD pipeline, and Power BI / Kusto dashboards.

---

## What to Do Next (Agent Instructions)

When asked to continue work, **resume from this context**.  
You should be able to:

1. Extend the .NET Worker or API for new modules (Graph Data Connect, Teams, SharePoint, etc.).
2. Add Managed Identity, Key Vault, or ADF pipeline integration.
3. Refactor for performance, compliance, or CI/CD.
4. Generate Terraform/Bicep scripts for Azure infrastructure.
5. Produce documentation or runbooks for deployment, audit, or DR.

---

## Style & Output Guidelines

- Use **C# (.NET 8)** for app code.
- Use **YAML / Bicep / Terraform** for IaC.
- Use **Markdown** for design docs and readmes.
- Keep outputs modular and professional (production-ready).

---

## Example Follow-up Prompts

> "Add delta query support for OneDrive and Mail in the HybridGraphCollectorWorker."  
> "Create a Terraform template for deploying this collector to Azure with Managed Identity."  
> "Generate a Power BI dashboard showing job volume, throughput, and 429 retry rates."  
> "Add a manifest signing step with SHA-256 + timestamp and store in Blob with immutability."  
> "Build the GDC trigger and poller module to finalize the end-to-end pipeline."

---

## Notes

- Assume the repo already includes the two projects (`HybridGraphCollectorWorker`, `EDiscoveryIntakeApi`) and the Docker bundle.
- The current goal is to **validate functionality first (POC)**, then harden for enterprise production.

## Current Project Structure

```
M365Test/
├── src/
│   ├── EDiscoveryIntakeApi/          # Web API service (✅ Running)
│   ├── HybridGraphCollectorWorker/   # Worker service
│   └── EDiscovery.Shared/            # Shared library
├── docker-compose.yml               # Container orchestration
├── README.md                        # Complete documentation
├── copilot.yaml                     # Agent manifest
├── test-requests.http              # API test samples
└── .env.template                   # Configuration template
```

## Current Status

- ✅ Solution built successfully
- ✅ EDiscovery API running on http://localhost:5230
- ✅ Swagger documentation available
- ✅ AutoRouter intelligence implemented
- ✅ Chain of custody with SHA-256 hashing
- ✅ Retry policies for Graph API throttling
- ⏳ Ready for Azure AD configuration and testing

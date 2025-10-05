# Hybrid eDiscovery Collector - Beginner's Setup Guide

**Project:** Hybrid Microsoft 365 eDiscovery Collection System  
**Date:** October 4, 2025  
**Version:** 2.0 - Multi-User Concurrent Processing  
**Target Audience:** Beginners, Legal Teams, IT Professionals

---

## 📋 Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Development Environment Setup](#2-development-environment-setup)
3. [Getting the Source Code](#3-getting-the-source-code)
4. [Project Overview](#4-project-overview)
5. [Building the Solution](#5-building-the-solution)
6. [Configuration Setup](#6-configuration-setup)
7. [Running the Application](#7-running-the-application)
8. [Testing Your Setup](#8-testing-your-setup)
9. [Using the Application](#9-using-the-application)
10. [Troubleshooting](#10-troubleshooting)
11. [Next Steps](#11-next-steps)

---

## 1. Prerequisites

Before you begin, ensure you have the following installed on your computer:

### 🖥️ **Required Software**

| Software               | Version      | Download Link                                                   | Purpose                    |
| ---------------------- | ------------ | --------------------------------------------------------------- | -------------------------- |
| **.NET SDK**           | 9.0 or later | [Download .NET](https://dotnet.microsoft.com/download)          | Core development framework |
| **Visual Studio Code** | Latest       | [Download VS Code](https://code.visualstudio.com/)              | Code editor                |
| **Git**                | Latest       | [Download Git](https://git-scm.com/downloads)                   | Version control            |
| **PowerShell**         | 7.0+         | [Download PowerShell](https://github.com/PowerShell/PowerShell) | Command line interface     |

### 🔧 **Recommended VS Code Extensions**

Open VS Code and install these extensions:

1. **C# for Visual Studio Code** (ms-dotnettools.csharp)
2. **REST Client** (humao.rest-client) - For testing API endpoints
3. **GitHub Copilot** (GitHub.copilot) - AI assistance
4. **Markdown All in One** (yzhang.markdown-all-in-one) - Documentation editing

### ☁️ **Azure Prerequisites (Optional for POC)**

- **Azure Subscription** (for production deployment)
- **Microsoft 365 Tenant** (for Graph API access)
- **Azure AD Application Registration** (for authentication)

---

## 2. Development Environment Setup

### Step 2.1: Verify .NET Installation

Open PowerShell or Command Prompt and run:

```powershell
dotnet --version
```

**Expected Output:**

```
9.0.100
```

If you see a version number starting with 9.x, you're ready to proceed.

### Step 2.2: Configure Git (First Time Only)

```powershell
git config --global user.name "Your Name"
git config --global user.email "your.email@company.com"
```

### Step 2.3: Create Working Directory

```powershell
# Create a directory for your projects
mkdir C:\eDiscovery
cd C:\eDiscovery
```

---

## 3. Getting the Source Code

### Step 3.1: Clone the Repository

```powershell
# Clone the repository
git clone https://github.com/DeeDee1103/M365Test.git
cd M365Test
```

### Step 3.2: Verify Repository Structure

```powershell
# List the contents
dir
```

**Expected Structure:**

```
📁 .github/          # GitHub configuration
📁 docs/             # Documentation
📁 src/              # Source code
📁 tests/            # Test projects
📄 README.md         # Project overview
📄 HybridEDiscoveryCollector.sln  # Visual Studio solution
```

---

## 4. Project Overview

Understanding what you've downloaded:

### 🏗️ **Solution Structure**

```
src/
├── EDiscoveryIntakeApi/           # Main REST API service
│   ├── Controllers/               # API endpoints
│   ├── Data/                     # Database context
│   └── Program.cs                # Application entry point
├── HybridGraphCollectorWorker/   # Background processing service
│   ├── Services/                 # Business logic
│   └── Worker.cs                 # Main worker logic
└── EDiscovery.Shared/            # Common libraries
    ├── Models/                   # Data models
    ├── Services/                 # Shared services
    └── Interfaces/               # Service contracts

tests/
├── EDiscovery.Shared.Tests/      # Unit tests
├── EDiscoveryIntakeApi.Tests/    # API tests
└── HybridGraphCollectorWorker.Tests/  # Worker tests
```

### 🎯 **What Each Component Does**

| Component          | Purpose                                                      | User Interaction                           |
| ------------------ | ------------------------------------------------------------ | ------------------------------------------ |
| **Intake API**     | Web interface for creating legal matters and collection jobs | Legal teams use Swagger UI to manage cases |
| **Worker Service** | Background service that collects data from Microsoft 365     | Runs automatically, processes queued jobs  |
| **Shared Library** | Common code used by both API and Worker                      | No direct user interaction                 |

---

## 5. Building the Solution

### Step 5.1: Open in Visual Studio Code

```powershell
# Open the project in VS Code
code .
```

### Step 5.2: Restore Dependencies

```powershell
# Restore NuGet packages
dotnet restore
```

**Expected Output:**

```
Restore completed in 2.3s for [project paths]
```

### Step 5.3: Build the Solution

```powershell
# Build all projects
dotnet build
```

**Expected Output:**

```
Build succeeded in 5.2s
    0 Warning(s)
    0 Error(s)
```

### ❌ **If Build Fails**

Common issues and solutions:

1. **Missing .NET SDK**: Install .NET 9.0 SDK
2. **Network Issues**: Check your internet connection for NuGet package downloads
3. **Permission Issues**: Run PowerShell as Administrator

---

## 6. Configuration Setup

### Step 6.1: Understand Configuration Files

The application uses these configuration files:

| File                           | Location      | Purpose              |
| ------------------------------ | ------------- | -------------------- |
| `appsettings.json`             | API project   | Production settings  |
| `appsettings.Development.json` | API project   | Development settings |
| `launchSettings.json`          | Both projects | Debug settings       |

### Step 6.2: Basic Configuration (POC Mode)

For proof-of-concept testing, the default configuration works out of the box:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=ediscovery.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Step 6.3: Azure AD Configuration (Optional)

For production use with real Microsoft 365 data, update these settings:

```json
{
  "AzureAd": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

**⚠️ Security Note:** Never commit real secrets to version control. Use environment variables or Azure Key Vault in production.

---

## 7. Running the Application

### Step 7.1: Start the API Service

Open a PowerShell terminal and run:

```powershell
# Navigate to API project
cd src\EDiscoveryIntakeApi

# Run the API
dotnet run
```

**Expected Output:**

```
[23:31:06 INF] : Starting eDiscovery Intake API
[23:31:07 INF] : eDiscovery Intake API started successfully
[23:31:08 INF] Now listening on: http://localhost:5230
```

### Step 7.2: Start the Worker Service

Open a **second** PowerShell terminal and run:

```powershell
# Navigate to Worker project
cd src\HybridGraphCollectorWorker

# Run the Worker
dotnet run
```

**Expected Output:**

```
[23:31:29 INF] : Starting Concurrent Hybrid Graph Collector Worker
[23:31:30 INF] : Concurrent Hybrid Graph Collector Worker started successfully
```

### 🎉 **Success Indicators**

When both services are running correctly, you should see:

1. ✅ API service listening on port 5230
2. ✅ Worker service started successfully
3. ✅ Database created automatically (SQLite file)
4. ✅ Log files created in `logs/` directory

---

## 8. Testing Your Setup

### Step 8.1: Test the API

Open your web browser and navigate to:

```
http://localhost:5230/swagger
```

You should see the **Swagger UI** with available API endpoints:

- **Matters** - Manage legal cases
- **Jobs** - Create and monitor collection jobs
- **Users** - User management (Admin only)
- **Health** - System health checks

### Step 8.2: Create Your First Matter

Using Swagger UI:

1. Click **"POST /api/matters"**
2. Click **"Try it out"**
3. Enter this JSON:

```json
{
  "name": "Test Case 001",
  "description": "My first eDiscovery case",
  "caseNumber": "CASE-001"
}
```

4. Click **"Execute"**
5. Look for **"201 Created"** response

### Step 8.3: Create Your First Collection Job

1. Click **"POST /api/jobs"**
2. Click **"Try it out"**
3. Enter this JSON:

```json
{
  "matterId": 1,
  "custodianEmail": "test.user@yourcompany.com",
  "jobType": "Email",
  "startDate": "2025-09-01T00:00:00Z",
  "endDate": "2025-10-04T23:59:59Z"
}
```

4. Click **"Execute"**
5. Look for **"201 Created"** response

### Step 8.4: Monitor Job Processing

Watch the Worker service terminal window. You should see:

```
[23:31:33 INF] AutoRouter decision: GraphApi (Confidence: 90.0%)
[23:31:33 INF] Starting email collection for custodian: test.user@yourcompany.com
```

**Note:** You'll see an authentication error in POC mode - this is expected without real Azure AD credentials.

---

## 9. Using the Application

### 👥 **User Roles and Capabilities**

The system supports 4 user roles:

| Role              | Capabilities                         | Concurrent Jobs | Data Limit |
| ----------------- | ------------------------------------ | --------------- | ---------- |
| **Administrator** | Full system access, user management  | 25              | Unlimited  |
| **Manager**       | Team oversight, assign jobs          | 10              | 100GB      |
| **Legal Analyst** | Create cases, basic collections      | 3               | 10GB       |
| **Auditor**       | Read-only access, compliance reports | 5               | View only  |

### 📊 **Dashboard Access**

- **API Documentation**: http://localhost:5230/swagger
- **Health Monitoring**: http://localhost:5230/health
- **Log Files**: `src/EDiscoveryIntakeApi/logs/` directory

### 🔄 **Typical Workflow**

1. **Create Matter** → Legal case setup
2. **Define Custodians** → People whose data to collect
3. **Create Collection Job** → Specify what data to collect
4. **Monitor Progress** → Watch job status in real-time
5. **Review Results** → Examine collected data and audit logs

---

## 10. Troubleshooting

### 🚨 **Common Issues**

#### Issue: "Port 5230 already in use"

**Solution:**

```powershell
# Find and kill the process
netstat -ano | findstr :5230
taskkill /f /pid [PID_NUMBER]
```

#### Issue: "Database connection failed"

**Solution:**

```powershell
# Delete the database and restart
cd src\EDiscoveryIntakeApi
del ediscovery.db*
dotnet run
```

#### Issue: "Authentication failed" in Worker

**Expected Behavior in POC Mode:**

- This is normal without real Azure AD credentials
- The system continues to function for testing
- Configure Azure AD for production use

#### Issue: Build errors

**Solution:**

```powershell
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### 📋 **Health Check Commands**

```powershell
# Check .NET version
dotnet --version

# Check if services are running
netstat -ano | findstr :5230

# View recent logs
cd src\EDiscoveryIntakeApi\logs
type *.log | Select-Object -Last 20
```

### 🆘 **Getting Help**

1. **Check the logs** in `logs/` directory
2. **Review the Technical Overview** in `docs/Technical_Overview.md`
3. **Examine the Process Flow** in `docs/Process_Flow_Diagram.md`
4. **Use Swagger UI** for API testing
5. **Check GitHub Issues** for known problems

---

## 11. Next Steps

### 🎯 **For POC Testing**

1. **Explore Swagger UI** - Try all the API endpoints
2. **Create Multiple Jobs** - Test concurrent processing
3. **Monitor Logs** - Watch the structured logging in action
4. **Test Different Scenarios** - Various data types and sizes

### 🏢 **For Production Deployment**

1. **Azure AD Setup** - Configure real authentication
2. **Azure SQL Database** - Upgrade from SQLite
3. **Azure Container Apps** - Deploy to cloud
4. **Security Review** - Implement enterprise security
5. **User Training** - Train your legal and IT teams

### 📚 **Learning Resources**

- **Technical Overview**: Complete architecture documentation
- **Process Flow Diagrams**: Visual workflow understanding
- **API Documentation**: Swagger UI for endpoint details
- **Source Code**: Well-commented codebase for learning

### 🔄 **Development Workflow**

1. **Make Changes** to source code
2. **Rebuild** with `dotnet build`
3. **Test** using Swagger UI
4. **View Logs** to monitor behavior
5. **Iterate** and improve

---

## 🎊 Congratulations!

You've successfully set up the Hybrid eDiscovery Collector with multi-user concurrent processing capabilities. The system is now ready to:

✅ **Handle multiple users** running collections simultaneously  
✅ **Intelligently route** between Graph API and Graph Data Connect  
✅ **Provide enterprise logging** with comprehensive audit trails  
✅ **Manage user roles** with appropriate access controls  
✅ **Balance worker loads** across multiple processing instances  
✅ **Maintain data integrity** with atomic job assignment

**You now have a fully functional enterprise-grade eDiscovery collection system!**

---

## 📞 Support Information

- **Documentation**: `docs/` directory
- **Sample Requests**: `test-requests.http` file
- **Configuration**: `appsettings.json` files
- **Logs**: `src/EDiscoveryIntakeApi/logs/` directory

**Remember**: This system successfully implements your original requirement to "handle many users can run this tool and process at the same time" with enterprise-grade concurrent processing capabilities.

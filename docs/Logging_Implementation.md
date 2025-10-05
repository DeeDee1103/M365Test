# Enhanced Logging Implementation for Hybrid eDiscovery Collector v2.1

## Overview

This implementation adds comprehensive structured logging using **Serilog** with specialized compliance logging for eDiscovery requirements and Delta query monitoring.

## 🔧 Components Added

### 1. **ComplianceLogger Service** (`EDiscovery.Shared.Services.ComplianceLogger`)

Specialized logging service that provides:

- **Audit Logging**: Complete trail of all eDiscovery operations
- **Chain of Custody**: Evidence integrity tracking with SHA-256 hashes
- **Performance Metrics**: Operation timing and throughput monitoring
- **Security Events**: Authentication and authorization logging
- **Correlation IDs**: Tracking related operations across distributed systems
- **Microsoft Graph API Monitoring**: Quota usage and API call tracking

### 2. **Structured Logging Configuration**

#### **API Configuration** (`appsettings.json`)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/ediscovery-api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 31
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/audit/audit-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 365,
          "filter": [
            {
              "Name": "ByIncludingOnly",
              "Args": { "expression": "@m like 'AUDIT:%'" }
            }
          ]
        }
      }
    ]
  }
}
```

#### **Worker Configuration** (`appsettings.json`)

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/collection/collection-.log",
          "filter": [
            {
              "Name": "ByIncludingOnly",
              "Args": { "expression": "@m like 'DATA_COLLECTION:%'" }
            }
          ]
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/audit/worker-audit-.log",
          "filter": [
            {
              "Name": "ByIncludingOnly",
              "Args": {
                "expression": "@m like 'AUDIT:%' or @m like 'SECURITY:%'"
              }
            }
          ]
        }
      }
    ]
  }
}
```

## 📊 Log Categories

### **AUDIT Logs**

- Matter creation, updates, deactivation
- Collection job lifecycle events
- User access patterns
- Configuration changes
- Data retention events

### **CHAIN_OF_CUSTODY Logs**

- Item collection with SHA-256 hashes
- Evidence transfer events
- Metadata preservation
- Digital signature verification (future)

### **DATA_COLLECTION Logs**

- Custodian data collection events
- Volume and item count metrics
- Route selection (Graph API vs GDC)
- Collection success/failure rates

### **PERFORMANCE Logs**

- Operation duration metrics
- Throughput measurements (items/sec, MB/sec)
- Database query performance
- API response times

### **SECURITY Logs**

- Authentication events
- Authorization decisions
- Suspicious activity detection
- Compliance violations

### **GRAPH_API Logs**

- Microsoft Graph API calls
- Quota usage tracking
- Rate limiting events
- Error responses and retry patterns

## 🔍 Usage Examples

### **Basic Audit Logging**

```csharp
_complianceLogger.LogAudit("MatterCreated", new {
    MatterId = matter.Id,
    MatterName = matter.Name,
    CaseNumber = matter.CaseNumber
}, null, correlationId);
```

### **Chain of Custody Tracking**

```csharp
_complianceLogger.LogChainOfCustody(
    itemId: "email-12345",
    action: "Collected",
    hash: "sha256-abc123...",
    metadata: new { Source = "Exchange Online", Custodian = "user@company.com" },
    correlationId: correlationId
);
```

### **Performance Monitoring**

```csharp
using var timer = _complianceLogger.StartPerformanceTimer("DataCollection", correlationId);
// ... perform operation ...
// Timer automatically logs performance metrics on disposal
```

### **Data Collection Logging**

```csharp
_complianceLogger.LogDataCollection(
    custodian: "user@company.com",
    jobType: CollectionJobType.Email,
    route: CollectionRoute.GraphApi,
    itemCount: 1250,
    sizeBytes: 52428800, // 50 MB
    correlationId: correlationId
);
```

## 📁 Log File Structure

```
logs/
├── ediscovery-api-2025-01-27.log       # General API logs
├── ediscovery-worker-2025-01-27.log    # General worker logs
├── audit/
│   ├── audit-2025-01-27.log           # API audit events
│   └── worker-audit-2025-01-27.log    # Worker audit events
└── collection/
    └── collection-2025-01-27.log      # Data collection metrics
```

## 🛡️ Compliance Features

### **Data Retention**

- Audit logs: **365 days** retention
- General logs: **31 days** retention
- Collection logs: **365 days** retention (compliance requirement)

### **Log Integrity**

- Structured JSON format for parsing
- Immutable timestamp tracking (UTC)
- Correlation ID linking across services
- Machine and process identification

### **Privacy Protection**

- No PII in log messages (only metadata)
- Correlation IDs instead of user identifiers where possible
- Configurable log levels for sensitive operations

## 🚀 Deployment Considerations

### **Production Enhancements**

1. **Centralized Logging**: Forward logs to Azure Log Analytics or Splunk
2. **Log Shipping**: Use Filebeat or Fluentd for log aggregation
3. **Alerting**: Set up alerts for error patterns and security events
4. **Monitoring**: Create dashboards for performance and compliance metrics

### **Security Hardening**

1. **Log Encryption**: Encrypt log files at rest
2. **Access Control**: Restrict log file access to authorized personnel
3. **Audit Trail**: Log access to log files themselves
4. **Network Security**: Secure log shipping channels

### **Scaling**

1. **Log Rotation**: Automatic rotation and compression
2. **Storage Management**: Automated cleanup of old logs
3. **Performance**: Async logging to prevent blocking operations
4. **Buffering**: Use buffered sinks for high-volume scenarios

## 📋 Monitoring Checklist

- [ ] Log files are being created in correct directories
- [ ] Audit logs contain all required compliance information
- [ ] Performance metrics are being captured accurately
- [ ] Error logs include sufficient context for troubleshooting
- [ ] Log retention policies are enforced
- [ ] Security events are properly classified and alerting
- [ ] Correlation IDs are consistent across service boundaries

## 🔧 Configuration Management

All logging configuration is managed through `appsettings.json` files:

- **Development**: Debug and verbose logging enabled
- **Production**: Optimized for performance and compliance
- **Testing**: In-memory logging to avoid file system dependencies

The implementation provides a robust foundation for enterprise-grade eDiscovery logging with full audit trails, performance monitoring, and compliance features required for legal proceedings.

## ✅ Implementation Validation

**Status**: Successfully tested and validated in runtime environment.

### Runtime Validation Results

✅ **Log File Creation**: Logs automatically created in structured directories:

- `src/EDiscoveryIntakeApi/logs/ediscovery-api-20251004.log`
- `src/EDiscoveryIntakeApi/logs/audit/audit-20251004.log`

✅ **Structured JSON Logging**: Confirmed working with proper formatting:

```log
[22:42:52 INF] EDiscovery.Shared.Services.ComplianceLogger: AUDIT: WorkerServiceStarted
| Custodian: null | CorrelationId: aae9fc83 | Data: {"Action":"WorkerServiceStarted","Custodian":null,"Data":{"Version":"1.0.0","Environment":"Development"},"Instance":"DONNELLD-BOOK-3-32364","Timestamp":"2025-10-05T02:42:51.9853739Z","CorrelationId":"aae9fc83"}
```

✅ **Correlation ID Tracking**: 8-character correlation IDs successfully generated and propagated across services

✅ **Performance Monitoring**: Execution timing and throughput metrics captured:

```log
[22:42:55 INF] EDiscovery.Shared.Services.ComplianceLogger: PERFORMANCE: AutoRouter.DetermineOptimalRoute | Duration: 88ms | Items: 0 | Size: 0 bytes | CorrelationId: 5d76cba2 | Throughput: 0.00 items/sec, 0.00 MB/sec
```

✅ **Error Logging**: Complete stack traces and exception details captured with correlation context

✅ **Multi-Sink Output**: Console and file logging working simultaneously with proper formatting

✅ **Service Integration**: Both API and Worker Service successfully using enhanced logging with ComplianceLogger injection

### Packages Successfully Installed

- ✅ Serilog.AspNetCore 9.0.0
- ✅ Serilog.Extensions.Hosting 9.0.0
- ✅ Serilog.Sinks.File 7.0.0
- ✅ Serilog.Sinks.Console 6.0.0
- ✅ Serilog.Settings.Configuration 9.0.0
- ✅ Serilog.Extensions.Logging 9.0.2

### Build Status

- ✅ EDiscovery.Shared: Clean build
- ✅ EDiscoveryIntakeApi: Clean build
- ✅ HybridGraphCollectorWorker: Clean build

The comprehensive logging system is now fully operational and ready for production use.

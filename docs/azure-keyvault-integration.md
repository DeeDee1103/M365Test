# Azure Key Vault Integration

## Overview

This document describes the Azure Key Vault integration using DefaultAzureCredential for secure configuration management in the M365 Hybrid eDiscovery solution.

## Features

- **DefaultAzureCredential**: Automatic credential chain for local development and production deployment
- **Secure Configuration**: Transparent fallback from Key Vault to local configuration
- **Multiple Authentication Methods**: Environment variables, Managed Identity, Azure CLI, Visual Studio
- **Production Ready**: Optimized credential options for different environments

## Authentication Methods (in order of precedence)

1. **Environment Variables** (`AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`)
2. **Managed Identity** (for Azure resources like App Service, VMs, etc.)
3. **Azure CLI** (`az login` credentials)
4. **Visual Studio Code** (Azure Account extension)
5. **Azure PowerShell** (`Connect-AzAccount` credentials)

## Configuration

### appsettings.json

```json
{
  "AzureKeyVault": {
    "VaultUrl": "https://your-keyvault-name.vault.azure.net/",
    "UseKeyVault": true,
    "TenantId": "",
    "ClientId": "",
    "EnableManagedIdentity": true
  }
}
```

### Environment Variables (Production)

```bash
# Service Principal Authentication
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
export AZURE_TENANT_ID="your-tenant-id"

# Key Vault Configuration
export AzureKeyVault__VaultUrl="https://your-keyvault-name.vault.azure.net/"
export AzureKeyVault__UseKeyVault="true"
```

## Secret Mapping

The system maps common configuration keys to standardized Key Vault secret names:

| Configuration Key              | Key Vault Secret Name              | Description                             |
| ------------------------------ | ---------------------------------- | --------------------------------------- |
| `GraphClientSecret`            | `ediscovery-graph-client-secret`   | Microsoft Graph API client secret       |
| `ServiceBusConnectionString`   | `ediscovery-servicebus-connection` | Azure Service Bus connection string     |
| `AzureStorageConnectionString` | `ediscovery-storage-connection`    | Azure Storage account connection string |
| `DatabaseConnectionString`     | `ediscovery-database-connection`   | SQL database connection string          |
| `SqlServerConnectionString`    | `ediscovery-sqlserver-connection`  | SQL Server connection string            |

## Usage Examples

### Basic Secret Retrieval

```csharp
public class ExampleService
{
    private readonly ISecureConfigurationService _secureConfig;

    public ExampleService(ISecureConfigurationService secureConfig)
    {
        _secureConfig = secureConfig;
    }

    public async Task<string> GetGraphClientSecretAsync()
    {
        // Will try Key Vault first, fallback to local configuration
        return await _secureConfig.GetSecretAsync("GraphClientSecret");
    }
}
```

### Connection String Management

```csharp
public async Task ConfigureServices()
{
    // Secure connection string retrieval
    var connectionString = _secureConfig.GetConnectionString("DefaultConnection");

    // Or specific service connection strings
    var serviceBusConnection = _secureConfig.GetServiceBusConnectionString();
    var storageConnection = _secureConfig.GetAzureStorageConnectionString();
}
```

### Key Vault Availability Check

```csharp
public async Task<bool> CheckKeyVaultStatus()
{
    var isAvailable = await _secureConfig.IsKeyVaultAvailableAsync();
    if (!isAvailable)
    {
        // Fallback to local configuration
        _logger.LogWarning("Key Vault not available, using local configuration");
    }
    return isAvailable;
}
```

## Development Setup

### Local Development (Without Key Vault)

1. Set `UseKeyVault: false` in `appsettings.Development.json`
2. Store secrets in local configuration files or user secrets
3. Use `dotnet user-secrets` for sensitive values:

```bash
dotnet user-secrets set "GraphClientSecret" "your-dev-secret"
dotnet user-secrets set "ServiceBusConnectionString" "your-dev-connection"
```

### Development with Key Vault

1. Install Azure CLI: `az login`
2. Set up Key Vault access permissions for your account
3. Configure `appsettings.Development.json`:

```json
{
  "AzureKeyVault": {
    "VaultUrl": "https://your-dev-keyvault.vault.azure.net/",
    "UseKeyVault": true,
    "EnableManagedIdentity": false
  }
}
```

## Production Deployment

### Azure App Service

1. Enable Managed Identity on App Service
2. Grant Key Vault access to the Managed Identity:

```bash
# Grant Key Vault access to App Service Managed Identity
az keyvault set-policy --name your-keyvault-name \
  --object-id <app-service-managed-identity-object-id> \
  --secret-permissions get list
```

3. Configure App Service settings:

```bash
az webapp config appsettings set --name your-app-name --resource-group your-rg \
  --settings AzureKeyVault__VaultUrl="https://your-keyvault.vault.azure.net/" \
             AzureKeyVault__UseKeyVault="true" \
             AzureKeyVault__EnableManagedIdentity="true"
```

### Azure Container Instances / Kubernetes

1. Use Service Principal or Managed Identity
2. Set environment variables:

```yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
        - name: ediscovery-api
          env:
            - name: AzureKeyVault__VaultUrl
              value: "https://your-keyvault.vault.azure.net/"
            - name: AzureKeyVault__UseKeyVault
              value: "true"
            - name: AZURE_CLIENT_ID
              valueFrom:
                secretKeyRef:
                  name: azure-credentials
                  key: client-id
```

## Security Best Practices

### Principle of Least Privilege

Grant only necessary permissions to Key Vault:

```bash
# Minimal permissions for application
az keyvault set-policy --name your-keyvault-name \
  --object-id <application-object-id> \
  --secret-permissions get list

# Do NOT grant: set, delete, purge, recover
```

### Network Security

1. **Private Endpoints**: Use private endpoints for Key Vault access
2. **Firewall Rules**: Restrict Key Vault access to specific IP ranges
3. **Trusted Services**: Enable access from trusted Microsoft services

```bash
# Configure Key Vault firewall
az keyvault network-rule add --name your-keyvault-name \
  --ip-address "your-app-service-outbound-ip"
```

### Audit and Monitoring

1. **Enable Key Vault Logging**:

```bash
# Enable diagnostics
az monitor diagnostic-settings create --name kv-diagnostics \
  --resource /subscriptions/.../resourceGroups/.../providers/Microsoft.KeyVault/vaults/your-keyvault \
  --logs '[{"category": "AuditEvent", "enabled": true}]' \
  --metrics '[{"category": "AllMetrics", "enabled": true}]' \
  --storage-account your-storage-account
```

2. **Monitor Access Patterns**: Set up alerts for unusual access patterns
3. **Regular Access Reviews**: Audit who has access to Key Vault secrets

## Troubleshooting

### Common Issues

**Authentication Failed**

```
Azure.Identity.CredentialUnavailableException: DefaultAzureCredential failed to retrieve a token
```

**Solutions:**

1. Verify `az login` status: `az account show`
2. Check environment variables: `echo $AZURE_CLIENT_ID`
3. Validate Key Vault permissions: `az keyvault secret show --vault-name your-kv --name test-secret`

**Secret Not Found**

```
Azure.RequestFailedException: The key vault does not exist or the user does not have permissions to access it.
```

**Solutions:**

1. Verify Key Vault URL is correct
2. Check secret name matches the mapping
3. Validate access policies

**Network Connectivity**

```
Azure.RequestFailedException: Name resolution failure
```

**Solutions:**

1. Check DNS resolution to `*.vault.azure.net`
2. Verify firewall rules allow outbound HTTPS
3. Test connectivity: `telnet your-keyvault.vault.azure.net 443`

### Diagnostic Commands

```bash
# Test Azure CLI authentication
az account show

# List Key Vault secrets (requires permissions)
az keyvault secret list --vault-name your-keyvault-name

# Test secret retrieval
az keyvault secret show --vault-name your-keyvault-name --name ediscovery-graph-client-secret

# Check Managed Identity status (from Azure resource)
curl -H "Metadata: true" "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://vault.azure.net/"
```

### Debug Logging

Enable detailed logging for authentication debugging:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Azure.Identity": "Debug",
        "Azure.Security.KeyVault": "Debug"
      }
    }
  }
}
```

## Migration Guide

### From Configuration Files to Key Vault

1. **Inventory Current Secrets**: List all sensitive configuration values
2. **Create Key Vault Secrets**:

```bash
# Create secrets in Key Vault
az keyvault secret set --vault-name your-kv --name ediscovery-graph-client-secret --value "your-secret"
az keyvault secret set --vault-name your-kv --name ediscovery-servicebus-connection --value "your-connection-string"
```

3. **Update Configuration**: Remove sensitive values from appsettings.json
4. **Test Fallback**: Verify local configuration still works when Key Vault is disabled
5. **Deploy Incrementally**: Enable Key Vault in stages (dev → staging → production)

### Rollback Plan

1. **Disable Key Vault**: Set `UseKeyVault: false` in configuration
2. **Restore Local Configuration**: Ensure all secrets are available locally
3. **Update Connection Strings**: Temporarily use local connection strings
4. **Monitor Application**: Verify functionality with local configuration

This Key Vault integration provides a secure, scalable approach to configuration management that follows Azure security best practices while maintaining operational flexibility.

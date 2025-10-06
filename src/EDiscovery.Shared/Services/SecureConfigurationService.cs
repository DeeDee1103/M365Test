using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Secure configuration service that prioritizes Azure Key Vault over local configuration
/// Provides transparent fallback from Key Vault to local appsettings.json
/// </summary>
public interface ISecureConfigurationService
{
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);
    Task<T?> GetConfigurationAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task<bool> IsKeyVaultAvailableAsync(CancellationToken cancellationToken = default);
    string GetConnectionString(string name);
    string GetGraphClientSecret();
    string GetServiceBusConnectionString();
    string GetAzureStorageConnectionString();
}

public class SecureConfigurationService : ISecureConfigurationService
{
    private readonly IAzureKeyVaultService _keyVaultService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecureConfigurationService> _logger;

    // Key Vault secret name mappings for common configuration values
    private readonly Dictionary<string, string> _secretMappings = new()
    {
        { "GraphClientSecret", "ediscovery-graph-client-secret" },
        { "ServiceBusConnectionString", "ediscovery-servicebus-connection" },
        { "AzureStorageConnectionString", "ediscovery-storage-connection" },
        { "DatabaseConnectionString", "ediscovery-database-connection" },
        { "SqlServerConnectionString", "ediscovery-sqlserver-connection" }
    };

    public SecureConfigurationService(
        IAzureKeyVaultService keyVaultService,
        IConfiguration configuration,
        ILogger<SecureConfigurationService> logger)
    {
        _keyVaultService = keyVaultService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        // Try Key Vault first with mapped secret name
        if (_secretMappings.TryGetValue(key, out var secretName))
        {
            var secretValue = await _keyVaultService.GetSecretAsync(secretName, cancellationToken);
            if (!string.IsNullOrEmpty(secretValue))
            {
                _logger.LogDebug("Retrieved secret from Key Vault: {Key}", key);
                return secretValue;
            }
        }

        // Try direct Key Vault lookup
        var directSecret = await _keyVaultService.GetSecretAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(directSecret))
        {
            _logger.LogDebug("Retrieved secret from Key Vault (direct): {Key}", key);
            return directSecret;
        }

        // Fallback to local configuration
        var localValue = _configuration[key];
        if (!string.IsNullOrEmpty(localValue))
        {
            _logger.LogDebug("Retrieved secret from local configuration: {Key}", key);
            return localValue;
        }

        _logger.LogWarning("Secret not found in Key Vault or local configuration: {Key}", key);
        return null;
    }

    public async Task<T?> GetConfigurationAsync<T>(string key, CancellationToken cancellationToken = default) 
        where T : class
    {
        try
        {
            // For complex configuration objects, prefer local configuration
            var localConfig = _configuration.GetSection(key).Get<T>();
            if (localConfig != null)
            {
                _logger.LogDebug("Retrieved configuration from local settings: {Key}", key);
                return localConfig;
            }

            // Try Key Vault for JSON-serialized configuration
            var secretValue = await GetSecretAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(secretValue))
            {
                var keyVaultConfig = System.Text.Json.JsonSerializer.Deserialize<T>(secretValue);
                if (keyVaultConfig != null)
                {
                    _logger.LogDebug("Retrieved configuration from Key Vault: {Key}", key);
                    return keyVaultConfig;
                }
            }

            _logger.LogWarning("Configuration not found: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve configuration: {Key}", key);
            return null;
        }
    }

    public async Task<bool> IsKeyVaultAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await _keyVaultService.IsAvailableAsync(cancellationToken);
    }

    public string GetConnectionString(string name)
    {
        // Try Key Vault mapping first
        var keyVaultKey = $"{name}ConnectionString";
        var result = GetSecretAsync(keyVaultKey).GetAwaiter().GetResult();
        
        if (!string.IsNullOrEmpty(result))
        {
            return result;
        }

        // Fallback to standard connection strings section
        var connectionString = _configuration.GetConnectionString(name);
        if (!string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException($"Connection string '{name}' not found in Key Vault or local configuration");
    }

    public string GetGraphClientSecret()
    {
        var secret = GetSecretAsync("GraphClientSecret").GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("Graph Client Secret not found in Key Vault or local configuration");
        }
        return secret;
    }

    public string GetServiceBusConnectionString()
    {
        var connectionString = GetSecretAsync("ServiceBusConnectionString").GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Service Bus connection string not found in Key Vault or local configuration");
        }
        return connectionString;
    }

    public string GetAzureStorageConnectionString()
    {
        var connectionString = GetSecretAsync("AzureStorageConnectionString").GetAwaiter().GetResult();
        if (string.IsNullOrEmpty(connectionString))
        {
            // Try fallback to local configuration
            connectionString = _configuration.GetConnectionString("AzureStorage");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Azure Storage connection string not found in Key Vault or local configuration");
            }
        }
        return connectionString;
    }
}
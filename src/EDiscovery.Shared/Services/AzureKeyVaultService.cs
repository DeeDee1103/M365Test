using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Azure Key Vault configuration service using DefaultAzureCredential
/// Provides secure configuration management with fallback to local settings
/// </summary>
public interface IAzureKeyVaultService
{
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllSecretsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public class AzureKeyVaultService : IAzureKeyVaultService
{
    private readonly SecretClient? _secretClient;
    private readonly ILogger<AzureKeyVaultService> _logger;
    private readonly string? _keyVaultUrl;
    private readonly bool _isEnabled;

    public AzureKeyVaultService(IConfiguration configuration, ILogger<AzureKeyVaultService> logger)
    {
        _logger = logger;
        _keyVaultUrl = configuration["AzureKeyVault:VaultUrl"];
        _isEnabled = !string.IsNullOrEmpty(_keyVaultUrl);

        if (_isEnabled && !string.IsNullOrEmpty(_keyVaultUrl))
        {
            try
            {
                // Use DefaultAzureCredential for authentication
                // This supports multiple authentication methods in order:
                // 1. Environment variables
                // 2. Managed Identity 
                // 3. Visual Studio
                // 4. Azure CLI
                // 5. Azure PowerShell
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeInteractiveBrowserCredential = true, // Disable interactive for production
                    ExcludeVisualStudioCodeCredential = false,  // Allow VS Code auth for development
                    ExcludeAzureCliCredential = false,          // Allow Azure CLI auth
                    ExcludeManagedIdentityCredential = false,   // Allow Managed Identity for production
                    ExcludeEnvironmentCredential = false        // Allow environment variables
                });

                _secretClient = new SecretClient(new Uri(_keyVaultUrl), credential);
                
                _logger.LogInformation("Azure Key Vault client initialized for vault: {VaultUrl}", _keyVaultUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Key Vault client for vault: {VaultUrl}", _keyVaultUrl);
                _isEnabled = false;
            }
        }
        else
        {
            _logger.LogInformation("Azure Key Vault is disabled - no VaultUrl configured");
        }
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _secretClient == null)
        {
            _logger.LogDebug("Key Vault not available, cannot retrieve secret: {SecretName}", secretName);
            return null;
        }

        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            _logger.LogDebug("Successfully retrieved secret: {SecretName}", secretName);
            return response.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret not found in Key Vault: {SecretName}", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret from Key Vault: {SecretName}", secretName);
            return null;
        }
    }

    public async Task<bool> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _secretClient == null)
        {
            _logger.LogWarning("Key Vault not available, cannot set secret: {SecretName}", secretName);
            return false;
        }

        try
        {
            await _secretClient.SetSecretAsync(secretName, secretValue, cancellationToken);
            _logger.LogInformation("Successfully set secret in Key Vault: {SecretName}", secretName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret in Key Vault: {SecretName}", secretName);
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetAllSecretsAsync(CancellationToken cancellationToken = default)
    {
        var secrets = new Dictionary<string, string>();

        if (!_isEnabled || _secretClient == null)
        {
            _logger.LogDebug("Key Vault not available, returning empty secrets collection");
            return secrets;
        }

        try
        {
            await foreach (var secretProperty in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken))
            {
                if (secretProperty.Enabled == true)
                {
                    var secret = await GetSecretAsync(secretProperty.Name, cancellationToken);
                    if (secret != null)
                    {
                        secrets[secretProperty.Name] = secret;
                    }
                }
            }

            _logger.LogInformation("Retrieved {Count} secrets from Key Vault", secrets.Count);
            return secrets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all secrets from Key Vault");
            return secrets;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _secretClient == null)
        {
            return false;
        }

        try
        {
            // Try to list secrets to verify connectivity and permissions
            var properties = _secretClient.GetPropertiesOfSecretsAsync(cancellationToken);
            await foreach (var _ in properties)
            {
                // If we can enumerate at least one secret, the connection works
                return true;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Key Vault connectivity check failed");
            return false;
        }
    }
}
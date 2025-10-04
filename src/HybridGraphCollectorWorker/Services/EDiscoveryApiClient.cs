using EDiscovery.Shared.Models;
using System.Text;
using System.Text.Json;

namespace HybridGraphCollectorWorker.Services;

public interface IEDiscoveryApiClient
{
    Task<CollectionJob?> GetJobAsync(int jobId);
    Task<bool> StartJobAsync(int jobId);
    Task<bool> CompleteJobAsync(int jobId, bool isSuccessful, long actualDataSizeBytes, int actualItemCount, string? manifestHash = null, string? errorMessage = null);
    Task<bool> RecordCollectedItemsAsync(int jobId, List<CollectedItem> items);
    Task<bool> LogJobEventAsync(int jobId, EDiscovery.Shared.Models.LogLevel level, string category, string message, string? details = null);
}

public class EDiscoveryApiClient : IEDiscoveryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EDiscoveryApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public EDiscoveryApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<EDiscoveryApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var baseUrl = configuration["EDiscoveryApi:BaseUrl"] ?? "https://localhost:7001";
        _httpClient.BaseAddress = new Uri(baseUrl);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<CollectionJob?> GetJobAsync(int jobId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/jobs/{jobId}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CollectionJob>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job {JobId}", jobId);
            return null;
        }
    }

    public async Task<bool> StartJobAsync(int jobId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/jobs/{jobId}/start", null);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to start job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully started job {JobId}", jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting job {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> CompleteJobAsync(int jobId, bool isSuccessful, long actualDataSizeBytes, int actualItemCount, string? manifestHash = null, string? errorMessage = null)
    {
        try
        {
            var request = new
            {
                IsSuccessful = isSuccessful,
                ActualDataSizeBytes = actualDataSizeBytes,
                ActualItemCount = actualItemCount,
                ManifestHash = manifestHash,
                ErrorMessage = errorMessage
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"api/jobs/{jobId}/complete", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to complete job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                return false;
            }

            _logger.LogInformation("Successfully completed job {JobId}", jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing job {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> RecordCollectedItemsAsync(int jobId, List<CollectedItem> items)
    {
        try
        {
            // Send items in batches to avoid large payloads
            const int batchSize = 100;
            
            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batch = items.Skip(i).Take(batchSize).ToList();
                
                var json = JsonSerializer.Serialize(batch, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"api/jobs/{jobId}/items", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to record items batch for job {JobId}. Status: {StatusCode}", jobId, response.StatusCode);
                    return false;
                }
            }

            _logger.LogInformation("Successfully recorded {ItemCount} items for job {JobId}", items.Count, jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording items for job {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> LogJobEventAsync(int jobId, EDiscovery.Shared.Models.LogLevel level, string category, string message, string? details = null)
    {
        try
        {
            // For simplicity in POC, we'll just log locally
            // In production, this could send structured logs to the API
            
            switch (level)
            {
                case EDiscovery.Shared.Models.LogLevel.Trace:
                case EDiscovery.Shared.Models.LogLevel.Debug:
                    _logger.LogDebug("[Job {JobId}] [{Category}] {Message} {Details}", jobId, category, message, details);
                    break;
                case EDiscovery.Shared.Models.LogLevel.Information:
                    _logger.LogInformation("[Job {JobId}] [{Category}] {Message} {Details}", jobId, category, message, details);
                    break;
                case EDiscovery.Shared.Models.LogLevel.Warning:
                    _logger.LogWarning("[Job {JobId}] [{Category}] {Message} {Details}", jobId, category, message, details);
                    break;
                case EDiscovery.Shared.Models.LogLevel.Error:
                case EDiscovery.Shared.Models.LogLevel.Critical:
                    _logger.LogError("[Job {JobId}] [{Category}] {Message} {Details}", jobId, category, message, details);
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging event for job {JobId}", jobId);
            return false;
        }
    }
}
using Microsoft.Graph.Models.ODataErrors;

namespace HybridGraphCollectorWorker.Services;

public class RetryPolicy
{
    private readonly ILogger _logger;
    private const int MaxRetries = 5;
    private static readonly TimeSpan[] RetryDelays = 
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    };

    public RetryPolicy(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ODataError ex) when (IsRetryableError(ex))
            {
                lastException = ex;
                
                if (attempt == MaxRetries)
                {
                    _logger.LogError(ex, "Max retry attempts reached for Graph API operation");
                    break;
                }

                var delay = GetRetryDelay(ex, attempt);
                _logger.LogWarning("Graph API throttling detected (attempt {Attempt}/{MaxAttempts}). Retrying after {DelaySeconds}s", 
                    attempt + 1, MaxRetries + 1, delay.TotalSeconds);
                
                await Task.Delay(delay);
            }
            catch (HttpRequestException ex) when (IsRetryableHttpError(ex))
            {
                lastException = ex;
                
                if (attempt == MaxRetries)
                {
                    _logger.LogError(ex, "Max retry attempts reached for HTTP operation");
                    break;
                }

                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                _logger.LogWarning("HTTP error detected (attempt {Attempt}/{MaxAttempts}). Retrying after {DelaySeconds}s", 
                    attempt + 1, MaxRetries + 1, delay.TotalSeconds);
                
                await Task.Delay(delay);
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed without exception");
    }

    private static bool IsRetryableError(ODataError ex)
    {
        // Retry on 429 (Too Many Requests) and 5xx server errors
        if (ex.ResponseStatusCode == 429) return true;
        if (ex.ResponseStatusCode >= 500 && ex.ResponseStatusCode < 600) return true;
        
        return false;
    }

    private static bool IsRetryableHttpError(HttpRequestException ex)
    {
        // Retry on network-related errors
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("timeout") || 
               message.Contains("connection") || 
               message.Contains("network");
    }

    private static TimeSpan GetRetryDelay(ODataError ex, int attempt)
    {
        // Check for Retry-After header (common in 429 responses)
        if (ex.ResponseStatusCode == 429)
        {
            // For Graph API, typically use exponential backoff with jitter
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
            return baseDelay + jitter;
        }

        return RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
    }
}
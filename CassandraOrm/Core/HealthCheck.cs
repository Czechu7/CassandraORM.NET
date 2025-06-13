using System;
using System.Threading;
using System.Threading.Tasks;
using Cassandra;
using Microsoft.Extensions.Logging;

namespace CassandraOrm.Core;

/// <summary>
/// Provides health check functionality for Cassandra connections
/// </summary>
public class CassandraHealthCheck
{
    private readonly ICluster _cluster;
    private readonly ILogger? _logger;

    public CassandraHealthCheck(ICluster cluster, ILogger? logger = null)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _logger = logger;
    }

    /// <summary>
    /// Checks if the Cassandra cluster is healthy and accessible
    /// </summary>
    /// <param name="timeout">Maximum time to wait for health check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        TimeSpan? timeout = null, 
        CancellationToken cancellationToken = default)
    {
        var checkTimeout = timeout ?? TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;

        try
        {
            using var timeoutCts = new CancellationTokenSource(checkTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            // Try to connect and execute a simple query
            using var session = await _cluster.ConnectAsync();
            
            // Execute a simple system query to verify connectivity
            var result = await session.ExecuteAsync(new SimpleStatement("SELECT release_version FROM system.local"));
            
            var version = result.FirstOrDefault()?.GetValue<string>("release_version");
            var duration = DateTime.UtcNow - startTime;

            _logger?.LogDebug("Cassandra health check successful. Version: {Version}, Duration: {Duration}ms", 
                version, duration.TotalMilliseconds);

            return new HealthCheckResult
            {
                IsHealthy = true,
                ResponseTime = duration,
                CassandraVersion = version,
                Message = "Cassandra cluster is healthy and responsive"
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = DateTime.UtcNow - startTime,
                Message = "Health check was cancelled"
            };
        }
        catch (TimeoutException)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger?.LogWarning("Cassandra health check timed out after {Duration}ms", duration.TotalMilliseconds);
            
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = duration,
                Message = $"Health check timed out after {duration.TotalMilliseconds:F0}ms"
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger?.LogError(ex, "Cassandra health check failed after {Duration}ms", duration.TotalMilliseconds);
            
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = duration,
                Message = $"Health check failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Waits for the Cassandra cluster to become available
    /// </summary>
    /// <param name="maxWaitTime">Maximum time to wait</param>
    /// <param name="retryInterval">Time between retry attempts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cluster becomes available, false if timeout</returns>
    public async Task<bool> WaitForClusterAsync(
        TimeSpan? maxWaitTime = null,
        TimeSpan? retryInterval = null,
        CancellationToken cancellationToken = default)
    {
        var maxWait = maxWaitTime ?? TimeSpan.FromMinutes(5);
        var interval = retryInterval ?? TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        _logger?.LogInformation("Waiting for Cassandra cluster to become available (max wait: {MaxWait})", maxWait);

        while (DateTime.UtcNow - startTime < maxWait && !cancellationToken.IsCancellationRequested)
        {
            var healthResult = await CheckHealthAsync(TimeSpan.FromSeconds(5), cancellationToken);
            
            if (healthResult.IsHealthy)
            {
                var totalWaitTime = DateTime.UtcNow - startTime;
                _logger?.LogInformation("Cassandra cluster is now available (waited {WaitTime})", totalWaitTime);
                return true;
            }

            _logger?.LogDebug("Cassandra cluster not yet available: {Message}. Retrying in {Interval}...", 
                healthResult.Message, interval);

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var finalWaitTime = DateTime.UtcNow - startTime;
        _logger?.LogWarning("Timed out waiting for Cassandra cluster after {WaitTime}", finalWaitTime);
        return false;
    }
}

/// <summary>
/// Result of a Cassandra health check
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Whether the cluster is healthy and responsive
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Response time for the health check
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Cassandra version if available
    /// </summary>
    public string? CassandraVersion { get; set; }

    /// <summary>
    /// Human-readable message about the health status
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception that occurred during health check (if any)
    /// </summary>
    public Exception? Exception { get; set; }

    public override string ToString()
    {
        return $"Healthy: {IsHealthy}, ResponseTime: {ResponseTime.TotalMilliseconds:F0}ms, Message: {Message}";
    }
}

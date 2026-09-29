using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace McpGateway.Core.Observability;

/// <summary>
/// Health check for Redis connectivity.
/// Checks if Redis connection is established and can respond to PING.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redisConnection;
    private readonly ILogger<RedisHealthCheck> _logger;

    // Default null lets the DI container treat IConnectionMultiplexer as optional when Redis is not configured (ValidateOnBuild would otherwise fail).
    public RedisHealthCheck(IConnectionMultiplexer? redisConnection = null, ILogger<RedisHealthCheck>? logger = null)
    {
        _redisConnection = redisConnection;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RedisHealthCheck>.Instance;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If no Redis connection configured, pass as healthy (using in-memory cache)
            if (_redisConnection == null)
            {
                return HealthCheckResult.Healthy("Redis not configured (in-memory cache used)");
            }

            // Check if connection is active
            if (!_redisConnection.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis connection is not established");
            }

            // Test Redis with PING command with timeout
            var db = _redisConnection.GetDatabase();
            var pingTask = db.PingAsync();
            var timeoutTask = Task.Delay(2000, cancellationToken); // 2 second timeout

            var completedTask = await Task.WhenAny(pingTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                return HealthCheckResult.Unhealthy("Redis ping operation timed out");
            }

            var pingResult = await pingTask;
            if (pingResult != TimeSpan.Zero)
            {
                return HealthCheckResult.Healthy($"Redis is responding (latency: {pingResult.TotalMilliseconds}ms)");
            }

            return HealthCheckResult.Unhealthy("Redis ping returned zero latency (unexpected)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy($"Redis health check failed: {ex.Message}");
        }
    }
}
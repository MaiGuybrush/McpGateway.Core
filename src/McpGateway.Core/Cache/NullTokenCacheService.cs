using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using McpGateway.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace McpGateway.Core.Cache;

/// <summary>
/// Null implementation of token cache service with in-memory fallback (degraded mode when Redis is unavailable).
/// This provides graceful degradation with local caching when Redis is down.
/// </summary>
public class NullTokenCacheService : ITokenCacheService
{
    private readonly ILogger<NullTokenCacheService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _inMemoryCache = new();
    private readonly IMemoryCache? _memoryCache;
    private bool _redisUnavailableLogged = false;

    public class CacheEntry
    {
        public ToolContext Context { get; set; } = default!;
        public DateTime Expiry { get; set; }
    }

    public NullTokenCacheService(ILogger<NullTokenCacheService> logger, IMemoryCache? memoryCache = null)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        
        if (!_redisUnavailableLogged)
        {
            _logger.LogWarning("Token cache fallback activated: Redis unavailable, using in-memory cache. Auth performance may be degraded.");
            _redisUnavailableLogged = true;
        }
    }

    public Task<ToolContext?> GetAsync(string tokenType, string token)
    {
        try
        {
            var cacheKey = GetCacheKey(tokenType, token);
            
            // Try IMemoryCache if available (preferred)
            if (_memoryCache != null)
            {
                if (_memoryCache.TryGetValue(cacheKey, out ToolContext? cachedContext) && cachedContext is not null)
                {
                    return Task.FromResult<ToolContext?>(cachedContext);
                }
            }
            
            // Fallback to concurrent dictionary
            if (_inMemoryCache.TryGetValue(cacheKey, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow)
                {
                    return Task.FromResult<ToolContext?>(entry.Context);
                }
                else
                {
                    // Remove expired entry
                    _inMemoryCache.TryRemove(cacheKey, out _);
                }
            }
            
            return Task.FromResult<ToolContext?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-memory cache lookup failed for {TokenType}", tokenType);
            return Task.FromResult<ToolContext?>(null);
        }
    }

    public Task SetAsync(string tokenType, string token, ToolContext context, int ttlMinutes)
    {
        try
        {
            var cacheKey = GetCacheKey(tokenType, token);
            var expiry = DateTime.UtcNow.AddMinutes(ttlMinutes);
            
            // Use IMemoryCache if available (preferred)
            if (_memoryCache != null)
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = expiry
                };
                _memoryCache.Set(cacheKey, context, options);
            }
            else
            {
                // Fallback to concurrent dictionary
                _inMemoryCache.AddOrUpdate(cacheKey, 
                    new CacheEntry { Context = context, Expiry = expiry },
                    (_, _) => new CacheEntry { Context = context, Expiry = expiry });
                
                // Clean up expired entries occasionally
                CleanupExpiredEntries();
            }
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-memory cache write failed for {TokenType}", tokenType);
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(string tokenType, string token)
    {
        try
        {
            var cacheKey = GetCacheKey(tokenType, token);
            
            // Remove from both caches
            _memoryCache?.Remove(cacheKey);
            _inMemoryCache.TryRemove(cacheKey, out _);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-memory cache removal failed for {TokenType}", tokenType);
            return Task.CompletedTask;
        }
    }

    private void CleanupExpiredEntries()
    {
        try
        {
            // Only cleanup when cache size exceeds threshold
            if (_inMemoryCache.Count > 1000)
            {
                var now = DateTime.UtcNow;
                var keysToRemove = _inMemoryCache
                    .Where(kvp => kvp.Value.Expiry < now)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in keysToRemove)
                {
                    _inMemoryCache.TryRemove(key, out _);
                }
                
                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired cache entries", keysToRemove.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache cleanup failed");
        }
    }

    private string GetCacheKey(string tokenType, string token)
    {
        // Format: auth:{tokenType}:{SHA256(token)}
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        
        return $"auth:{tokenType}:{tokenHash}";
    }
}
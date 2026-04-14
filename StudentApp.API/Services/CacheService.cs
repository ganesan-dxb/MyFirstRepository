using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace StudentApp.API.Services;

// Simple wrapper around IDistributedCache (backed by Redis)
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(60);

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var data = await _cache.GetStringAsync(key);
            if (data is null) return default;
            _logger.LogInformation("Cache HIT for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry
            };
            var data = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, data, options);
            _logger.LogInformation("Cache SET for key: {Key}, expiry: {Expiry}", key, expiry ?? DefaultExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Cache REMOVE for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key: {Key}", key);
        }
    }

    // Removes all keys with a given prefix (e.g. "students:")
    // Note: IDistributedCache doesn't support pattern delete natively,
    // so we track prefixed keys via a Redis Set in production.
    // For simplicity here, we remove known keys by name.
    public async Task RemoveByPrefixAsync(string prefix)
    {
        // In a real app, use IConnectionMultiplexer.GetServer().Keys(pattern)
        // For this sample, we clear the common list key
        await RemoveAsync($"{prefix}:all");
    }
}

using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace StudentApp.Gateway.RateLimiting;

// Stores rate limit counters in Redis so they work correctly when
// you run multiple Gateway instances (distributed rate limiting).
//
// Uses a Redis sorted set per IP:
//   Key:    ratelimit:{ip}:{endpoint-bucket}
//   Score:  Unix timestamp of the request
//   Member: unique request id
// We then count members in the sliding window to decide allow/deny.
public class RedisRateLimitStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitStore> _logger;

    public RedisRateLimitStore(IConnectionMultiplexer redis, ILogger<RedisRateLimitStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    // Returns true if the request is allowed, false if rate limit exceeded.
    // windowSeconds: length of the sliding window
    // limit:         max requests allowed in the window
    public async Task<bool> IsAllowedAsync(string ip, string bucket, int windowSeconds, int limit)
    {
        try
        {
            var db   = _redis.GetDatabase();
            var key  = $"ratelimit:{bucket}:{ip}";
            var now  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var from = now - (windowSeconds * 1000L);

            // Atomic Lua script — avoids race conditions between check and write
            const string script = """
                local key    = KEYS[1]
                local now    = tonumber(ARGV[1])
                local from   = tonumber(ARGV[2])
                local limit  = tonumber(ARGV[3])
                local window = tonumber(ARGV[4])
                redis.call('ZREMRANGEBYSCORE', key, '-inf', from)
                local count = redis.call('ZCARD', key)
                if count < limit then
                    redis.call('ZADD', key, now, now .. '-' .. math.random(1,99999))
                    redis.call('EXPIRE', key, window)
                    return 1
                end
                return 0
                """;

            var result = (int)await db.ScriptEvaluateAsync(
                script,
                keys:   new RedisKey[]  { key },
                values: new RedisValue[] { now, from, limit, windowSeconds });

            return result == 1;
        }
        catch (Exception ex)
        {
            // If Redis is down, fail open (allow the request) — don't block users
            _logger.LogWarning(ex, "Rate limit Redis check failed for {Ip}, failing open", ip);
            return true;
        }
    }
}

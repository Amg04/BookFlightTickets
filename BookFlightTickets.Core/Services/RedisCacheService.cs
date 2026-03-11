using BookFlightTickets.Core.ServiceContracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookFlightTickets.Core.Services
{
    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly IConnectionMultiplexer _redisConnection;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheService(IDistributedCache cache,
            ILogger<RedisCacheService> logger,
            IConnectionMultiplexer redisConnection = null)
        {
            _cache = cache;
            _logger = logger;
            _redisConnection = redisConnection;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true, 
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region GetAsync

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var cachedData = await _cache.GetStringAsync(key);

                if (string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key); 
                    return default; 
                }

                _logger.LogDebug("Cache hit for key: {Key}", key);

                var result = JsonSerializer.Deserialize<T>(cachedData, _jsonOptions);

                if (result is null)
                {
                    _logger.LogWarning("Deserialization returned null for key: {Key}. Data: {Data}",
                        key, cachedData.Substring(0, Math.Min(cachedData.Length, 100)));
                    return default;
                }

                return result;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "JSON deserialization failed for key: {Key}. Removing corrupted data.", key);
                await _cache.RemoveAsync(key); 
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache for key: {Key}", key);
                return default; // Fallback to database
            }
        }

        #endregion

        #region SetAsync

        public async Task SetAsync<T>(
            string key,
            T value, 
            TimeSpan? expiry = null)
        {
            try
            {
                if (value is null)
                {
                    _logger.LogWarning("Attempted to cache null value for key: {Key}", key);
                    return;
                }

                var serializedData = JsonSerializer.Serialize(value, _jsonOptions);

                var options = new DistributedCacheEntryOptions();

                if (expiry.HasValue)
                {
                    options.SetAbsoluteExpiration(expiry.Value);
                    _logger.LogDebug("Cache set for key: {Key} with absolute expiry: {Expiry}",
                        key, expiry.Value);
                }
                else
                {
                    options.SetSlidingExpiration(TimeSpan.FromMinutes(10));
                    _logger.LogDebug("Cache set for key: {Key} with sliding expiry", key);
                }

                await _cache.SetStringAsync(key, serializedData, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            }
        }

        #endregion

        #region RemoveAsync

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
                _logger.LogDebug("Cache removed for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache for key: {Key}", key);
            }
        }

        #endregion

        #region ExistsAsync

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var data = await _cache.GetStringAsync(key);
                return !string.IsNullOrEmpty(data);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking existence for key: {Key}", key);
                return false;
            }
        }

        #endregion

        #region GetOrSetAsync

        public async Task<T?> GetOrSetAsync<T>(
            string key, 
            Func<Task<T>> factory,
            TimeSpan? expiry = null)
        {
            var cached = await GetAsync<T>(key);

            if (cached is not null)
                return cached;

            var value = await factory();

            if (value is not null)
                await SetAsync(key, value, expiry);

            return value;
        }

        #endregion

        #region RemoveByPatternAsync

        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                _logger.LogInformation("Pattern deletion requested for: {Pattern}", pattern);

                if (_redisConnection != null)
                {
                    var endpoints = _redisConnection.GetEndPoints();
                    var server = _redisConnection.GetServer(endpoints.First());

                    var keys = server.Keys(pattern: pattern).ToArray();

                    if (keys.Any())
                    {
                        await _redisConnection.GetDatabase().KeyDeleteAsync(keys);
                        _logger.LogInformation("Deleted {KeyCount} keys matching pattern: {Pattern}",
                            keys.Length, pattern);
                    }
                    else
                    {
                        _logger.LogDebug("No keys found matching pattern: {Pattern}", pattern);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "RemoveByPatternAsync requires IConnectionMultiplexer. Pattern: {Pattern}",
                        pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache by pattern: {Pattern}", pattern);
            }
        }

        #endregion

    }
}
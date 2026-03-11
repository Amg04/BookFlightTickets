namespace BookFlightTickets.Core.ServiceContracts
{
    public interface IRedisCacheService
    {
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task<T?> GetOrSetAsync<T>(
            string key, 
            Func<Task<T>> factory,
            TimeSpan? expiry = null);
        Task RemoveByPatternAsync(string pattern);
    }
}

namespace Graphify.Cache;

/// <summary>
/// Provides caching functionality for graph operations.
/// </summary>
public interface ICacheProvider
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Stores a value in the cache with the specified key.
    /// </summary>
    Task SetAsync<T>(string key, T value) where T : class;

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Invalidates a cached entry by key.
    /// </summary>
    Task InvalidateAsync(string key);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    Task ClearAsync();
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace Graphify.Cache;

/// <summary>
/// File-based semantic cache using SHA256 content hashing.
/// Stores extraction results in .graphify/cache/ directory.
/// </summary>
public sealed class SemanticCache : ICacheProvider
{
    private readonly string _cacheDirectory;
    private readonly string _indexFilePath;
    private readonly ConcurrentDictionary<string, CacheEntry> _index;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public SemanticCache(string projectRoot = ".")
    {
        _cacheDirectory = Path.Combine(projectRoot, ".graphify", "cache");
        _indexFilePath = Path.Combine(_cacheDirectory, "index.json");
        _index = new ConcurrentDictionary<string, CacheEntry>();
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureCacheDirectoryExists();
        LoadIndexAsync().GetAwaiter().GetResult();
    }

    private void EnsureCacheDirectoryExists()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Computes SHA256 hash of file content.
    /// </summary>
    public async Task<string> ComputeHashAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Checks if a file has changed since it was cached.
    /// </summary>
    public async Task<bool> IsChangedAsync(string filePath)
    {
        if (!_index.TryGetValue(filePath, out var entry))
        {
            return true; // Not cached yet
        }

        if (!File.Exists(filePath))
        {
            return true; // File no longer exists
        }

        var currentHash = await ComputeHashAsync(filePath);
        return currentHash != entry.ContentHash;
    }

    /// <summary>
    /// Retrieves cached extraction result if file is unchanged.
    /// </summary>
    public async Task<T?> GetCachedResultAsync<T>(string filePath) where T : class
    {
        if (!_index.TryGetValue(filePath, out var entry))
        {
            return null;
        }

        var isChanged = await IsChangedAsync(filePath);
        if (isChanged)
        {
            return null;
        }

        if (!File.Exists(entry.ResultFilePath))
        {
            // Cache entry exists but result file missing - invalidate
            await InvalidateAsync(filePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(entry.ResultFilePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            // Corrupt cache file - invalidate
            await InvalidateAsync(filePath);
            return null;
        }
    }

    /// <summary>
    /// Caches extraction result with current file hash.
    /// </summary>
    public async Task CacheResultAsync<T>(string filePath, T result) where T : class
    {
        var hash = await ComputeHashAsync(filePath);
        var resultFileName = $"{hash}.json";
        var resultFilePath = Path.Combine(_cacheDirectory, resultFileName);

        var json = JsonSerializer.Serialize(result, _jsonOptions);
        await File.WriteAllTextAsync(resultFilePath, json);

        var entry = new CacheEntry(
            FilePath: filePath,
            ContentHash: hash,
            CachedAt: DateTimeOffset.UtcNow,
            ResultFilePath: resultFilePath
        );

        _index[filePath] = entry;
        await SaveIndexAsync();
    }

    // ICacheProvider implementation

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (!_index.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (!File.Exists(entry.ResultFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(entry.ResultFilePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value) where T : class
    {
        var hash = ComputeKeyHash(key);
        var resultFileName = $"{hash}.json";
        var resultFilePath = Path.Combine(_cacheDirectory, resultFileName);

        var json = JsonSerializer.Serialize(value, _jsonOptions);
        await File.WriteAllTextAsync(resultFilePath, json);

        var entry = new CacheEntry(
            FilePath: key,
            ContentHash: hash,
            CachedAt: DateTimeOffset.UtcNow,
            ResultFilePath: resultFilePath
        );

        _index[key] = entry;
        await SaveIndexAsync();
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_index.ContainsKey(key));
    }

    public async Task InvalidateAsync(string key)
    {
        if (_index.TryRemove(key, out var entry))
        {
            try
            {
                if (File.Exists(entry.ResultFilePath))
                {
                    File.Delete(entry.ResultFilePath);
                }
            }
            catch (IOException)
            {
                // Ignore file deletion errors
            }

            await SaveIndexAsync();
        }
    }

    public async Task ClearAsync()
    {
        await _indexLock.WaitAsync();
        try
        {
            _index.Clear();

            if (Directory.Exists(_cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        // Ignore file deletion errors
                    }
                }
            }

            await SaveIndexAsync();
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task LoadIndexAsync()
    {
        if (!File.Exists(_indexFilePath))
        {
            return;
        }

        await _indexLock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(_indexFilePath);
            var entries = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json, _jsonOptions);
            
            if (entries != null)
            {
                foreach (var kvp in entries)
                {
                    _index[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (JsonException)
        {
            // Corrupt index file - start fresh
            _index.Clear();
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task SaveIndexAsync()
    {
        await _indexLock.WaitAsync();
        try
        {
            var entries = _index.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(entries, _jsonOptions);
            await File.WriteAllTextAsync(_indexFilePath, json);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static string ComputeKeyHash(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

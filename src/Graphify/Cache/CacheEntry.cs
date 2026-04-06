namespace Graphify.Cache;

/// <summary>
/// Represents a cache entry with file metadata.
/// </summary>
public record CacheEntry(
    string FilePath,
    string ContentHash,
    DateTimeOffset CachedAt,
    string ResultFilePath
);

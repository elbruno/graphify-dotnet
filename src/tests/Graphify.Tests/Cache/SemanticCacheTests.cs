using System.Text.Json;
using Graphify.Cache;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Cache;

public sealed class SemanticCacheTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SemanticCache _cache;

    public SemanticCacheTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
        _cache = new SemanticCache(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task ComputeHashAsync_SameContent_ReturnsSameHash()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "Hello, World!");

        // Act
        var hash1 = await _cache.ComputeHashAsync(filePath);
        var hash2 = await _cache.ComputeHashAsync(filePath);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public async Task ComputeHashAsync_DifferentContent_ReturnsDifferentHash()
    {
        // Arrange
        var filePath1 = Path.Combine(_testRoot, "test1.txt");
        var filePath2 = Path.Combine(_testRoot, "test2.txt");
        await File.WriteAllTextAsync(filePath1, "Hello, World!");
        await File.WriteAllTextAsync(filePath2, "Goodbye, World!");

        // Act
        var hash1 = await _cache.ComputeHashAsync(filePath1);
        var hash2 = await _cache.ComputeHashAsync(filePath2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _cache.ComputeHashAsync(filePath));
    }

    [Fact]
    public async Task CacheResultAsync_AndRetrieve_ReturnsOriginalData()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.cs");
        await File.WriteAllTextAsync(filePath, "class Test { }");
        
        var testData = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "Test", Label = "Test", FileType = FileType.Code, SourceFile = filePath }
            },
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = filePath
        };

        // Act
        await _cache.CacheResultAsync(filePath, testData);
        var retrieved = await _cache.GetCachedResultAsync<ExtractionResult>(filePath);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Nodes);
        Assert.Equal("Test", retrieved.Nodes[0].Id);
    }

    [Fact]
    public async Task IsChangedAsync_UnchangedFile_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "content");
        var testData = new { Value = "test" };
        await _cache.CacheResultAsync(filePath, testData);

        // Act
        var isChanged = await _cache.IsChangedAsync(filePath);

        // Assert
        Assert.False(isChanged);
    }

    [Fact]
    public async Task IsChangedAsync_ModifiedFile_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "original content");
        var testData = new { Value = "test" };
        await _cache.CacheResultAsync(filePath, testData);

        // Act
        await Task.Delay(10); // Ensure timestamp difference
        await File.WriteAllTextAsync(filePath, "modified content");
        var isChanged = await _cache.IsChangedAsync(filePath);

        // Assert
        Assert.True(isChanged);
    }

    [Fact]
    public async Task IsChangedAsync_NotCached_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "content");

        // Act
        var isChanged = await _cache.IsChangedAsync(filePath);

        // Assert
        Assert.True(isChanged);
    }

    [Fact]
    public async Task IsChangedAsync_DeletedFile_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "content");
        var testData = new { Value = "test" };
        await _cache.CacheResultAsync(filePath, testData);

        // Act
        File.Delete(filePath);
        var isChanged = await _cache.IsChangedAsync(filePath);

        // Assert
        Assert.True(isChanged);
    }

    [Fact]
    public async Task GetCachedResultAsync_MissingResultFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "content");
        var testData = new { Value = "test" };
        await _cache.CacheResultAsync(filePath, testData);

        // Delete the cached result file
        var cacheDir = Path.Combine(_testRoot, ".graphify", "cache");
        foreach (var file in Directory.GetFiles(cacheDir, "*.json"))
        {
            if (!file.EndsWith("index.json"))
            {
                File.Delete(file);
            }
        }

        // Act
        var result = await _cache.GetCachedResultAsync<object>(filePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCachedResultAsync_CorruptCache_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "test.txt");
        await File.WriteAllTextAsync(filePath, "content");
        var testData = new { Value = "test" };
        await _cache.CacheResultAsync(filePath, testData);

        // Corrupt the cached result file
        var cacheDir = Path.Combine(_testRoot, ".graphify", "cache");
        foreach (var file in Directory.GetFiles(cacheDir, "*.json"))
        {
            if (!file.EndsWith("index.json"))
            {
                await File.WriteAllTextAsync(file, "invalid json {{{");
            }
        }

        // Act
        var result = await _cache.GetCachedResultAsync<object>(filePath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_AndGetAsync_RoundTrip()
    {
        // Arrange
        var key = "test-key";
        var value = new { Name = "Test", Count = 42 };

        // Act
        await _cache.SetAsync(key, value);
        var retrieved = await _cache.GetAsync<object>(key);

        // Assert
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task ExistsAsync_ExistingKey_ReturnsTrue()
    {
        // Arrange
        var key = "test-key";
        var value = new { Name = "Test" };
        await _cache.SetAsync(key, value);

        // Act
        var exists = await _cache.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        var key = "nonexistent-key";

        // Act
        var exists = await _cache.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesEntry()
    {
        // Arrange
        var key = "test-key";
        var value = new { Name = "Test" };
        await _cache.SetAsync(key, value);

        // Act
        await _cache.InvalidateAsync(key);
        var exists = await _cache.ExistsAsync(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        // Arrange
        await _cache.SetAsync("key1", new { Value = 1 });
        await _cache.SetAsync("key2", new { Value = 2 });
        await _cache.SetAsync("key3", new { Value = 3 });

        // Act
        await _cache.ClearAsync();

        // Assert
        Assert.False(await _cache.ExistsAsync("key1"));
        Assert.False(await _cache.ExistsAsync("key2"));
        Assert.False(await _cache.ExistsAsync("key3"));
    }

    [Fact]
    public async Task CacheRecovery_LoadsExistingIndex()
    {
        // Arrange
        var key = "test-key";
        var value = new { Name = "Test" };
        await _cache.SetAsync(key, value);

        // Act - Create new cache instance with same root
        var newCache = new SemanticCache(_testRoot);
        var exists = await newCache.ExistsAsync(key);

        // Assert
        Assert.True(exists);
    }
}

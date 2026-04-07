using Graphify.Cache;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class CacheIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public CacheIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [Fact(Timeout = 30000)]
    public async Task Cache_SaveAndReload_PreservesEntries()
    {
        // Arrange: create a cache and add entries
        var cache1 = new SemanticCache(_tempDir);
        var testData = new TestPayload("hello", 42);

        await cache1.SetAsync("key1", testData);
        await cache1.SetAsync("key2", new TestPayload("world", 99));

        // Verify entries exist in original cache
        Assert.True(await cache1.ExistsAsync("key1"));
        Assert.True(await cache1.ExistsAsync("key2"));

        // Act: create a NEW cache from the same directory (simulates process restart)
        var cache2 = new SemanticCache(_tempDir);

        // Assert: entries survive reload
        Assert.True(await cache2.ExistsAsync("key1"), "key1 should persist across cache instances");
        Assert.True(await cache2.ExistsAsync("key2"), "key2 should persist across cache instances");

        var reloaded = await cache2.GetAsync<TestPayload>("key1");
        Assert.NotNull(reloaded);
        Assert.Equal("hello", reloaded.Name);
        Assert.Equal(42, reloaded.Value);

        _output.WriteLine("Cache entries survived reload successfully");
    }

    [Fact(Timeout = 30000)]
    public async Task Cache_DetectsFileChanges()
    {
        // Arrange: create a file and cache it
        var testFile = Path.Combine(_tempDir, "source.cs");
        await File.WriteAllTextAsync(testFile, "public class Original {}");

        var cache = new SemanticCache(_tempDir);
        var originalHash = await cache.ComputeHashAsync(testFile);
        await cache.CacheResultAsync(testFile, new TestPayload("original", 1));

        // Verify not changed initially
        Assert.False(await cache.IsChangedAsync(testFile), "File should not be marked changed initially");

        // Act: modify the file
        await File.WriteAllTextAsync(testFile, "public class Modified { int x = 42; }");

        // Assert: cache reports it as changed (SHA256 mismatch)
        var isChanged = await cache.IsChangedAsync(testFile);
        Assert.True(isChanged, "Cache should detect file content change via SHA256");

        var newHash = await cache.ComputeHashAsync(testFile);
        Assert.NotEqual(originalHash, newHash);

        _output.WriteLine($"Original hash: {originalHash[..16]}..., New hash: {newHash[..16]}...");
    }

    [Fact(Timeout = 30000)]
    public async Task Cache_ClearAndReload_IsEmpty()
    {
        // Arrange
        var cache = new SemanticCache(_tempDir);
        await cache.SetAsync("alpha", new TestPayload("a", 1));
        await cache.SetAsync("beta", new TestPayload("b", 2));
        Assert.True(await cache.ExistsAsync("alpha"));
        Assert.True(await cache.ExistsAsync("beta"));

        // Act: clear the cache
        await cache.ClearAsync();

        // Assert: cache is empty
        Assert.False(await cache.ExistsAsync("alpha"), "alpha should be gone after clear");
        Assert.False(await cache.ExistsAsync("beta"), "beta should be gone after clear");

        // Act: reload from disk
        var cache2 = new SemanticCache(_tempDir);
        Assert.False(await cache2.ExistsAsync("alpha"), "alpha should not survive clear+reload");
        Assert.False(await cache2.ExistsAsync("beta"), "beta should not survive clear+reload");

        _output.WriteLine("Cache clear and reload verified empty");
    }

    private sealed record TestPayload(string Name, int Value);
}

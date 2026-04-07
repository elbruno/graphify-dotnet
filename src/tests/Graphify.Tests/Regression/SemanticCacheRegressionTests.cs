using Graphify.Cache;
using Xunit;

namespace Graphify.Tests.Regression;

/// <summary>
/// Regression tests for Bug 1: SemanticCache.ClearAsync Deadlock.
/// Root cause: ClearAsync() acquired _indexLock (SemaphoreSlim(1,1)) then called
/// SaveIndexAsync() which tried to acquire the same lock. SemaphoreSlim is NOT
/// reentrant → deadlock. Fix: extracted SaveIndexCoreAsync() (lockless) for internal use.
/// </summary>
[Trait("Category", "Regression")]
public sealed class SemanticCacheRegressionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SemanticCache _cache;

    public SemanticCacheRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _cache = new SemanticCache(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Regression Bug 1: ClearAsync must not deadlock. If it completes within 5s, the
    /// SemaphoreSlim reentrance bug is fixed.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task ClearAsync_DoesNotDeadlock_RegressionBug1()
    {
        var clearTask = _cache.ClearAsync();
        var completed = await Task.WhenAny(clearTask, Task.Delay(5000));

        Assert.Same(clearTask, completed);
        await clearTask; // Propagate exceptions
    }

    /// <summary>
    /// Regression Bug 1: After ClearAsync, SaveIndexAsync (public, lock-acquiring)
    /// must succeed — proving the lock was properly released.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task ClearAsync_ThenSaveAsync_NoDeadlock_RegressionBug1()
    {
        await _cache.ClearAsync();

        // SetAsync internally calls SaveIndexAsync (the public one that acquires the lock)
        var saveTask = _cache.SetAsync("key1", new TestPayload("value1"));
        var completed = await Task.WhenAny(saveTask, Task.Delay(5000));

        Assert.Same(saveTask, completed);
        await saveTask;
    }

    /// <summary>
    /// Regression Bug 1: ClearAsync and SetAsync (which calls AddAsync path) running
    /// concurrently must not deadlock under lock contention.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task ClearAsync_ConcurrentWithAddAsync_NoDeadlock_RegressionBug1()
    {
        var clearTask = _cache.ClearAsync();
        var addTask = _cache.SetAsync("concurrent-key", new TestPayload("concurrent-value"));

        var allDone = Task.WhenAll(clearTask, addTask);
        var completed = await Task.WhenAny(allDone, Task.Delay(5000));

        Assert.Same(allDone, completed);
        await allDone;
    }

    /// <summary>
    /// Regression Bug 1: Multiple sequential ClearAsync calls prove the lock is
    /// properly released each time.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task MultipleSequentialClears_NoDeadlock_RegressionBug1()
    {
        for (var i = 0; i < 10; i++)
        {
            await _cache.ClearAsync();
        }
    }

    /// <summary>
    /// Regression Bug 1: Add many entries then clear — stress test under heavier workload.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task ClearAsync_UnderLoad_NoDeadlock_RegressionBug1()
    {
        for (var i = 0; i < 100; i++)
        {
            await _cache.SetAsync($"key-{i}", new TestPayload($"value-{i}"));
        }

        var clearTask = _cache.ClearAsync();
        var completed = await Task.WhenAny(clearTask, Task.Delay(5000));

        Assert.Same(clearTask, completed);
        await clearTask;
    }

    /// <summary>
    /// Regression Bug 1: The public SaveIndexAsync (via SetAsync) still works independently.
    /// This tests the lock-acquiring path directly.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SaveAsync_CalledExternally_NoDeadlock_RegressionBug1()
    {
        await _cache.SetAsync("ext-key", new TestPayload("ext-value"));

        var result = await _cache.GetAsync<TestPayload>("ext-key");
        Assert.NotNull(result);
        Assert.Equal("ext-value", result.Value);
    }

    private sealed record TestPayload(string Value);
}

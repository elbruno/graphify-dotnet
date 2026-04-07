using Xunit;

namespace Graphify.Tests.Pipeline;

/// <summary>
/// Tests for WatchMode file-watching pipeline feature.
/// All tests are commented out until WatchMode implementation lands in Graphify.Pipeline.
/// Tests use temp directories and have timeouts to prevent hanging.
/// </summary>
public sealed class WatchModeTests : IDisposable
{
    private readonly string _testRoot;

    public WatchModeTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "graphify_watch_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
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
    [Trait("Category", "Pipeline")]
    public void TestRoot_DirectoryExists_ForTestInfrastructure()
    {
        // Validates test infrastructure is working
        Assert.True(Directory.Exists(_testRoot));
    }

    // ──────────────────────────────────────────────
    // WatchMode tests
    // TODO: Uncomment when WatchMode lands in Graphify.Pipeline
    // Expected class shape:
    //   public class WatchMode : IDisposable
    //   {
    //       public WatchMode(string path, TimeSpan? debounceInterval = null);
    //       public Task WatchAsync(Func<string, Task> onChange, CancellationToken ct = default);
    //   }
    // ──────────────────────────────────────────────

    // [Fact]
    // [Trait("Category", "Pipeline")]
    // public void Constructor_ValidPath_CreatesInstance()
    // {
    //     using var watcher = new WatchMode(_testRoot);
    //     Assert.NotNull(watcher);
    // }

    // [Fact]
    // [Trait("Category", "Pipeline")]
    // public void Constructor_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    // {
    //     var fakePath = Path.Combine(_testRoot, "does_not_exist");
    //
    //     Assert.Throws<DirectoryNotFoundException>(() =>
    //         new WatchMode(fakePath));
    // }

    // [Fact]
    // [Trait("Category", "Pipeline")]
    // public void WatchMode_ImplementsIDisposable()
    // {
    //     var watcher = new WatchMode(_testRoot);
    //     Assert.IsAssignableFrom<IDisposable>(watcher);
    //     watcher.Dispose(); // Should not throw
    // }

    // [Fact(Timeout = 10000)]
    // [Trait("Category", "Pipeline")]
    // public async Task WatchAsync_CancellationToken_StopsWatching()
    // {
    //     using var watcher = new WatchMode(_testRoot);
    //     using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    //
    //     var changedFiles = new List<string>();
    //     await watcher.WatchAsync(
    //         path => { changedFiles.Add(path); return Task.CompletedTask; },
    //         cts.Token);
    //
    //     // Should exit cleanly when token is cancelled, not throw
    //     Assert.NotNull(changedFiles); // Just verify we got here
    // }

    // [Fact(Timeout = 10000)]
    // [Trait("Category", "Pipeline")]
    // public async Task WatchAsync_FileCreated_IsDetected()
    // {
    //     using var watcher = new WatchMode(_testRoot);
    //     using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    //     var detectedChanges = new List<string>();
    //
    //     var watchTask = watcher.WatchAsync(
    //         path => { detectedChanges.Add(path); return Task.CompletedTask; },
    //         cts.Token);
    //
    //     // Give the watcher time to start
    //     await Task.Delay(100);
    //
    //     // Create a file — should be detected
    //     var testFile = Path.Combine(_testRoot, "test.cs");
    //     await File.WriteAllTextAsync(testFile, "// test content");
    //
    //     // Wait for detection
    //     await Task.Delay(1000);
    //     cts.Cancel();
    //
    //     try { await watchTask; } catch (OperationCanceledException) { }
    //
    //     Assert.Contains(detectedChanges, p => p.Contains("test.cs"));
    // }

    // [Fact(Timeout = 10000)]
    // [Trait("Category", "Pipeline")]
    // public async Task WatchAsync_RapidChanges_AreDebounced()
    // {
    //     // Debounce interval of 500ms — rapid writes within that window should coalesce
    //     using var watcher = new WatchMode(_testRoot, debounceInterval: TimeSpan.FromMilliseconds(500));
    //     using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    //     var callbackCount = 0;
    //
    //     var watchTask = watcher.WatchAsync(
    //         path => { Interlocked.Increment(ref callbackCount); return Task.CompletedTask; },
    //         cts.Token);
    //
    //     await Task.Delay(100); // Let watcher start
    //
    //     // Rapid-fire 10 writes to the same file within ~50ms
    //     var testFile = Path.Combine(_testRoot, "rapid.cs");
    //     for (int i = 0; i < 10; i++)
    //     {
    //         await File.WriteAllTextAsync(testFile, $"// change {i}");
    //         await Task.Delay(5);
    //     }
    //
    //     // Wait for debounce to settle
    //     await Task.Delay(1500);
    //     cts.Cancel();
    //
    //     try { await watchTask; } catch (OperationCanceledException) { }
    //
    //     // With debouncing, 10 rapid writes should produce far fewer callbacks
    //     Assert.True(callbackCount >= 1, "Should detect at least one change");
    //     Assert.True(callbackCount < 10, $"Debounce should coalesce — got {callbackCount} callbacks for 10 writes");
    // }
}

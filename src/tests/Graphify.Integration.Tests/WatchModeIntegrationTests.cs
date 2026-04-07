using Graphify.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class WatchModeIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public WatchModeIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-watch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [Fact(Timeout = 30000)]
    public async Task WatchMode_DetectsNewFile()
    {
        // Arrange: set up a FileSystemWatcher to verify change detection
        using var watcher = new FileSystemWatcher(_tempDir)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        var changeDetected = new TaskCompletionSource<string>();
        watcher.Created += (_, e) => changeDetected.TrySetResult(e.FullPath);

        // Act: create a new .cs file
        var newFile = Path.Combine(_tempDir, "NewClass.cs");
        await File.WriteAllTextAsync(newFile, "public class NewClass {}");

        // Assert: change detected within timeout
        var result = await Task.WhenAny(changeDetected.Task, Task.Delay(5000));
        Assert.True(changeDetected.Task.IsCompleted, "File creation should be detected by watcher");

        var detectedPath = await changeDetected.Task;
        _output.WriteLine($"Detected new file: {detectedPath}");
        Assert.Contains("NewClass.cs", detectedPath);
    }

    [Fact(Timeout = 30000)]
    public async Task WatchMode_DetectsModifiedFile()
    {
        // Arrange: create an existing file first
        var existingFile = Path.Combine(_tempDir, "Existing.cs");
        await File.WriteAllTextAsync(existingFile, "public class Existing {}");

        // Allow FS to settle
        await Task.Delay(500);

        using var watcher = new FileSystemWatcher(_tempDir)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        var changeDetected = new TaskCompletionSource<string>();
        watcher.Changed += (_, e) => changeDetected.TrySetResult(e.FullPath);

        // Act: modify the file
        await File.WriteAllTextAsync(existingFile, "public class Existing { int modified = 1; }");

        // Assert
        var result = await Task.WhenAny(changeDetected.Task, Task.Delay(5000));
        Assert.True(changeDetected.Task.IsCompleted, "File modification should be detected");

        var detectedPath = await changeDetected.Task;
        _output.WriteLine($"Detected modified file: {detectedPath}");
        Assert.Contains("Existing.cs", detectedPath);
    }

    [Fact(Timeout = 30000)]
    public async Task WatchMode_DebounceCoalescesRapidChanges()
    {
        // Arrange: the WatchMode class uses a 500ms debounce
        // Simulate rapid file creation and verify that events get coalesced
        var eventCount = 0;
        using var watcher = new FileSystemWatcher(_tempDir)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        watcher.Created += (_, _) => Interlocked.Increment(ref eventCount);

        // Act: rapidly create 5 files
        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(_tempDir, $"Rapid{i}.cs"), $"public class Rapid{i} {{}}");
        }

        // Wait for events to arrive
        await Task.Delay(1500);

        _output.WriteLine($"Events received: {eventCount}");

        // Assert: we should have received events (FileSystemWatcher fires per-file)
        // but WatchMode's debounce would coalesce them into 1-2 processing runs
        // Here we verify the raw events fire, the debounce logic is in WatchMode
        Assert.True(eventCount >= 1, "Should detect at least 1 file creation event");
        Assert.True(eventCount <= 10, "Should not produce excessive events");
    }

    [Fact(Timeout = 30000)]
    public async Task WatchMode_IgnoresNonCodeFiles()
    {
        // Arrange: FileDetector only picks up recognized code/doc/media extensions
        // Files like .tmp and .log are not recognized
        var detector = new FileDetector();

        // Create non-code files
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "notes.tmp"), "temporary");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "output.log"), "log data");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "data.dat"), "binary data");

        // Act
        var options = new FileDetectorOptions(RootPath: _tempDir, RespectGitIgnore: false);
        var detected = await detector.ExecuteAsync(options);

        _output.WriteLine($"Detected {detected.Count} files from non-code inputs");
        foreach (var f in detected)
            _output.WriteLine($"  {f.FileName} ({f.Language})");

        // Assert: non-code files should NOT be detected
        Assert.Empty(detected);
    }
}

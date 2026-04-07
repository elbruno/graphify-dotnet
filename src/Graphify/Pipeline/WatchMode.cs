using System.Collections.Concurrent;
using Graphify.Cache;
using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Pipeline;

/// <summary>
/// Watches a directory for file changes and incrementally re-processes only modified files.
/// Uses SHA256 cache to detect truly changed content (not just timestamps).
/// </summary>
public sealed class WatchMode : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly SemanticCache _cache;
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
    private readonly TextWriter _output;
    private readonly bool _verbose;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pendingChanges = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);

    private KnowledgeGraph? _currentGraph;

    public WatchMode(TextWriter output, bool verbose = false)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _verbose = verbose;
        _cache = new SemanticCache();
        _watcher = new FileSystemWatcher
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size
                         | NotifyFilters.CreationTime
        };
    }

    /// <summary>
    /// Sets the initial graph from a prior pipeline run. Call before WatchAsync.
    /// </summary>
    public void SetInitialGraph(KnowledgeGraph graph)
    {
        _currentGraph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    /// <summary>
    /// Starts watching the target path and incrementally re-processes on each detected change.
    /// The caller must run the initial pipeline and call <see cref="SetInitialGraph"/> first.
    /// </summary>
    public async Task WatchAsync(string path, string output, string[] formats, CancellationToken ct)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            await _output.WriteLineAsync($"Error: Directory not found: {fullPath}");
            return;
        }

        if (_currentGraph is null)
        {
            await _output.WriteLineAsync("Error: No initial graph set. Run the pipeline first.");
            return;
        }

        await _output.WriteLineAsync($"Watching {fullPath} for changes... (Ctrl+C to stop)");
        await _output.WriteLineAsync();

        // Configure watcher
        _watcher.Path = fullPath;
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.EnableRaisingEvents = true;

        // Process loop — drains pending changes with debounce
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_debounceDelay, ct);

                if (_pendingChanges.IsEmpty)
                    continue;

                // Snapshot and clear pending changes
                var changedFiles = _pendingChanges.Keys.ToList();
                foreach (var key in changedFiles)
                    _pendingChanges.TryRemove(key, out _);

                await ProcessChangesAsync(changedFiles, fullPath, output, formats, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }

        await _output.WriteLineAsync();
        await _output.WriteLineAsync("Watch stopped.");
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        // Skip hidden/cache directories
        if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.", StringComparison.Ordinal) ||
            e.FullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            e.FullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _pendingChanges[e.FullPath] = DateTimeOffset.UtcNow;
    }

    private async Task ProcessChangesAsync(
        List<string> changedPaths,
        string rootPath,
        string outputDir,
        string[] formats,
        CancellationToken ct)
    {
        if (!await _processLock.WaitAsync(0, ct))
            return; // Already processing

        try
        {
            // Filter to files that actually changed content (via SHA256)
            var trulyChanged = new List<string>();
            foreach (var filePath in changedPaths)
            {
                if (!File.Exists(filePath))
                {
                    trulyChanged.Add(filePath); // deleted file counts as changed
                    continue;
                }

                try
                {
                    if (await _cache.IsChangedAsync(filePath))
                        trulyChanged.Add(filePath);
                }
                catch
                {
                    trulyChanged.Add(filePath); // can't check → treat as changed
                }
            }

            if (trulyChanged.Count == 0)
            {
                if (_verbose)
                    await _output.WriteLineAsync("  (no content changes detected)");
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            await _output.WriteLineAsync($"[{timestamp}] Change detected in {trulyChanged.Count} file(s):");
            foreach (var f in trulyChanged.Take(10))
            {
                var rel = Path.GetRelativePath(rootPath, f);
                var exists = File.Exists(f);
                await _output.WriteLineAsync($"  {(exists ? "~" : "✗")} {rel}");
            }
            if (trulyChanged.Count > 10)
                await _output.WriteLineAsync($"  ... and {trulyChanged.Count - 10} more");

            // Re-detect and extract only changed files
            var fileDetector = new FileDetector();
            var options = new FileDetectorOptions(
                RootPath: rootPath,
                MaxFileSizeBytes: 1024 * 1024,
                RespectGitIgnore: true);

            var allDetected = await fileDetector.ExecuteAsync(options, ct);

            // Filter to changed files only
            var changedSet = new HashSet<string>(trulyChanged, StringComparer.OrdinalIgnoreCase);
            var filesToProcess = allDetected.Where(d => changedSet.Contains(d.FilePath)).ToList();

            if (filesToProcess.Count == 0)
            {
                await _output.WriteLineAsync("  (no processable files in change set)");
                return;
            }

            // Extract
            var extractor = new Extractor();
            var newResults = new List<ExtractionResult>();
            foreach (var file in filesToProcess)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = await extractor.ExecuteAsync(file, ct);
                    if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                        newResults.Add(result);
                }
                catch (Exception ex)
                {
                    if (_verbose)
                        await _output.WriteLineAsync($"  Warning: extraction failed for {file.RelativePath}: {ex.Message}");
                }
            }

            if (newResults.Count == 0)
            {
                await _output.WriteLineAsync("  (no extractable content)");
                return;
            }

            // Rebuild graph by merging new extraction results into a fresh build
            var graphBuilder = new GraphBuilder(new GraphBuilderOptions
            {
                CreateFileNodes = true,
                MinEdgeWeight = 0.1,
                MergeStrategy = MergeStrategy.MostRecent
            });

            var incrementalGraph = await graphBuilder.ExecuteAsync(newResults, ct);
            _currentGraph!.MergeGraph(incrementalGraph);

            // Re-cluster
            var clusterEngine = new ClusterEngine(new ClusterOptions
            {
                MaxIterations = 100,
                Resolution = 1.0,
                MinSplitSize = 5,
                MaxCommunityFraction = 0.2
            });
            _currentGraph = await clusterEngine.ExecuteAsync(_currentGraph, ct);

            // Re-export
            Directory.CreateDirectory(outputDir);
            foreach (var format in formats)
            {
                var outputPath = Path.Combine(outputDir, $"graph.{format}");
                switch (format.ToLowerInvariant())
                {
                    case "json":
                        await new JsonExporter().ExportAsync(_currentGraph, outputPath, ct);
                        break;
                    case "html":
                        await new HtmlExporter().ExportAsync(_currentGraph, outputPath, cancellationToken: ct);
                        break;
                }
            }

            await _output.WriteLineAsync($"  Re-processed {newResults.Count} file(s) → {_currentGraph.NodeCount} nodes, {_currentGraph.EdgeCount} edges");
            await _output.WriteLineAsync($"  Exported to {outputDir}");
            await _output.WriteLineAsync();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _output.WriteLineAsync($"  Error during incremental update: {ex.Message}");
            if (_verbose)
                await _output.WriteLineAsync(ex.StackTrace ?? string.Empty);
        }
        finally
        {
            _processLock.Release();
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _processLock.Dispose();
    }
}

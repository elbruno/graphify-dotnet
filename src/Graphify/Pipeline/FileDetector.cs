using System.Diagnostics;
using System.Runtime.CompilerServices;
using Graphify.Models;

namespace Graphify.Pipeline;

/// <summary>
/// First stage of the pipeline: discovers all processable files in a directory tree.
/// </summary>
public class FileDetector : IPipelineStage<FileDetectorOptions, IReadOnlyList<DetectedFile>>
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".py", ".ts", ".tsx", ".js", ".jsx", ".go", ".rs", ".java", ".c", ".cpp", ".h", ".hpp",
        ".rb", ".cs", ".kt", ".scala", ".php", ".swift", ".r", ".lua", ".sh", ".bash", ".ps1",
        ".yaml", ".yml", ".json", ".toml", ".xml"
    };

    private static readonly HashSet<string> DocumentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".rst", ".adoc"
    };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg"
    };

    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = "Python",
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".go"] = "Go",
        [".rs"] = "Rust",
        [".java"] = "Java",
        [".c"] = "C",
        [".cpp"] = "C++",
        [".h"] = "C",
        [".hpp"] = "C++",
        [".rb"] = "Ruby",
        [".cs"] = "CSharp",
        [".kt"] = "Kotlin",
        [".scala"] = "Scala",
        [".php"] = "PHP",
        [".swift"] = "Swift",
        [".r"] = "R",
        [".lua"] = "Lua",
        [".sh"] = "Shell",
        [".bash"] = "Shell",
        [".ps1"] = "PowerShell",
        [".yaml"] = "YAML",
        [".yml"] = "YAML",
        [".json"] = "JSON",
        [".toml"] = "TOML",
        [".xml"] = "XML",
        [".md"] = "Markdown",
        [".txt"] = "Text",
        [".rst"] = "ReStructuredText",
        [".adoc"] = "AsciiDoc",
        [".pdf"] = "PDF",
        [".png"] = "PNG",
        [".jpg"] = "JPEG",
        [".jpeg"] = "JPEG",
        [".webp"] = "WebP",
        [".gif"] = "GIF",
        [".svg"] = "SVG"
    };

    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "venv", ".venv", "env", ".env", "node_modules", "__pycache__", ".git",
        "dist", "build", "target", "out", "bin", "obj", "site-packages", "lib64",
        ".pytest_cache", ".mypy_cache", ".ruff_cache", ".tox", ".eggs"
    };

    public async Task<IReadOnlyList<DetectedFile>> ExecuteAsync(
        FileDetectorOptions input,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(input.RootPath))
        {
            throw new DirectoryNotFoundException($"Root path not found: {input.RootPath}");
        }

        var rootPath = Path.GetFullPath(input.RootPath);

        // Validate root scan directory
        var validator = new Graphify.Security.InputValidator();
        var pathValidation = validator.ValidatePath(rootPath);
        if (!pathValidation.IsValid)
        {
            throw new ArgumentException($"Invalid root path: {string.Join("; ", pathValidation.Errors)}");
        }

        var gitTrackedFiles = input.RespectGitIgnore
            ? await GetGitTrackedFilesAsync(rootPath, cancellationToken)
            : null;

        var detectedFiles = new List<DetectedFile>();
        var tasks = new List<Task<DetectedFile?>>();

        await foreach (var filePath in EnumerateFilesAsync(rootPath, input, gitTrackedFiles, cancellationToken))
        {
            tasks.Add(ProcessFileAsync(filePath, rootPath, input, cancellationToken));

            if (tasks.Count >= 50)
            {
                var batch = await Task.WhenAll(tasks);
                detectedFiles.AddRange(batch.Where(f => f != null)!);
                tasks.Clear();
            }
        }

        if (tasks.Count > 0)
        {
            var batch = await Task.WhenAll(tasks);
            detectedFiles.AddRange(batch.Where(f => f != null)!);
        }

        return detectedFiles.OrderBy(f => f.RelativePath).ToList();
    }

    private async IAsyncEnumerable<string> EnumerateFilesAsync(
        string rootPath,
        FileDetectorOptions options,
        HashSet<string>? gitTrackedFiles,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDir = queue.Dequeue();

            string[] files;
            string[] directories;

            try
            {
                files = Directory.GetFiles(currentDir);
                directories = Directory.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                
                if (dirName.StartsWith('.') || IsSkipDirectory(dirName))
                {
                    continue;
                }

                // FINDING-005: Skip symlinks to prevent following links outside the project
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }

                    // Verify resolved path is still within root
                    var resolvedDir = Path.GetFullPath(dir);
                    if (!resolvedDir.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }

                queue.Enqueue(dir);
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                
                if (fileName.StartsWith('.'))
                {
                    continue;
                }

                // FINDING-005: Skip symlinked files
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }

                    var resolvedFile = Path.GetFullPath(file);
                    if (!resolvedFile.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }

                if (gitTrackedFiles != null && !gitTrackedFiles.Contains(file))
                {
                    continue;
                }

                yield return file;
            }

            await Task.Yield();
        }
    }

    private async Task<DetectedFile?> ProcessFileAsync(
        string filePath,
        string rootPath,
        FileDetectorOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
            {
                return null;
            }

            if (fileInfo.Length > options.MaxFileSizeBytes)
            {
                return null;
            }

            var extension = fileInfo.Extension.ToLowerInvariant();

            if (options.IncludeExtensions != null &&
                !options.IncludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            var category = ClassifyFile(extension);
            if (!category.HasValue)
            {
                return null;
            }

            if (options.ExcludePatterns != null)
            {
                var relativePath = Path.GetRelativePath(rootPath, filePath);
                if (options.ExcludePatterns.Any(pattern => 
                    relativePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }
            }

            var language = ExtensionToLanguage.TryGetValue(extension, out var lang) 
                ? lang 
                : extension.TrimStart('.');

            var relPath = Path.GetRelativePath(rootPath, filePath);

            return new DetectedFile(
                FilePath: filePath,
                FileName: fileInfo.Name,
                Extension: extension,
                Language: language,
                Category: category.Value,
                SizeBytes: fileInfo.Length,
                RelativePath: relPath
            );
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static FileCategory? ClassifyFile(string extension)
    {
        if (CodeExtensions.Contains(extension))
        {
            return FileCategory.Code;
        }

        if (DocumentationExtensions.Contains(extension))
        {
            return FileCategory.Documentation;
        }

        if (MediaExtensions.Contains(extension))
        {
            return FileCategory.Media;
        }

        return null;
    }

    private static bool IsSkipDirectory(string dirName)
    {
        if (SkipDirectories.Contains(dirName))
        {
            return true;
        }

        if (dirName.EndsWith("_venv", StringComparison.OrdinalIgnoreCase) ||
            dirName.EndsWith("_env", StringComparison.OrdinalIgnoreCase) ||
            dirName.EndsWith(".egg-info", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static async Task<HashSet<string>?> GetGitTrackedFilesAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files",
                WorkingDirectory = rootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return null;
            }

            var trackedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(rootPath, trimmed));
                    trackedFiles.Add(fullPath);
                }
            }

            return trackedFiles;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

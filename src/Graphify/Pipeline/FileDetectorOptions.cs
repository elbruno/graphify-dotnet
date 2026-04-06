namespace Graphify.Pipeline;

/// <summary>
/// Configuration options for file detection.
/// </summary>
/// <param name="RootPath">The root directory to scan for files.</param>
/// <param name="MaxFileSizeBytes">Maximum file size in bytes (default: 1MB).</param>
/// <param name="ExcludePatterns">Patterns of paths to exclude from detection.</param>
/// <param name="IncludeExtensions">Specific extensions to include (null = all supported).</param>
/// <param name="RespectGitIgnore">Whether to respect .gitignore patterns (default: true).</param>
public record FileDetectorOptions(
    string RootPath,
    long MaxFileSizeBytes = 1_048_576, // 1MB default
    string[]? ExcludePatterns = null,
    string[]? IncludeExtensions = null,
    bool RespectGitIgnore = true
);

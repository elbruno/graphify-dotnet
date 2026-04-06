namespace Graphify.Models;

/// <summary>
/// Represents a file detected during the discovery phase.
/// </summary>
/// <param name="FilePath">The absolute path to the file.</param>
/// <param name="FileName">The name of the file (including extension).</param>
/// <param name="Extension">The file extension (e.g., ".cs").</param>
/// <param name="Language">The programming language or format (e.g., "CSharp", "Python").</param>
/// <param name="Category">The category of the file (Code, Documentation, Media).</param>
/// <param name="SizeBytes">The size of the file in bytes.</param>
/// <param name="RelativePath">The path relative to the root directory.</param>
public record DetectedFile(
    string FilePath,
    string FileName,
    string Extension,
    string Language,
    FileCategory Category,
    long SizeBytes,
    string RelativePath
);

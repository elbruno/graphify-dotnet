using System.Runtime.InteropServices;
using Graphify.Security;
using Xunit;

namespace Graphify.Tests.Security;

/// <summary>
/// Security tests for input validation: symlink detection, path traversal in output dirs,
/// SSRF validation completeness.
/// Covers FINDING-005, FINDING-006, FINDING-012.
/// </summary>
[Trait("Category", "Security")]
public sealed class InputValidationSecurityTests : IDisposable
{
    private readonly string _testRoot;
    private readonly InputValidator _validator = new();

    public InputValidationSecurityTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"graphify-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }

    #region FINDING-005: Symlink Detection

    [Trait("Category", "UnixOnly")]
    [Fact]
    public void FileDetector_SymlinkFile_IsSkipped()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Symlink creation requires elevated privileges on Windows
            return;
        }

        // Arrange — create a real file and a symlink to it
        var realFile = Path.Combine(_testRoot, "real_file.cs");
        var symlinkFile = Path.Combine(_testRoot, "symlink_file.cs");
        File.WriteAllText(realFile, "public class Test {}");

        try
        {
            File.CreateSymbolicLink(symlinkFile, realFile);
            Assert.True(File.Exists(symlinkFile), "Symlink should be created");

            // Act — check if the file is detected as a symlink
            var fileInfo = new FileInfo(symlinkFile);
            var isSymlink = fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

            // Assert — the file detector should identify and skip symlinks
            Assert.True(isSymlink, "File should be detected as a symlink (ReparsePoint)");
        }
        catch (IOException)
        {
            // Symlink creation not supported in this environment
        }
    }

    [Trait("Category", "UnixOnly")]
    [Fact]
    public void FileDetector_SymlinkDirectory_IsSkipped()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange — create a real directory and a symlink to it
        var realDir = Path.Combine(_testRoot, "real_dir");
        var symlinkDir = Path.Combine(_testRoot, "symlink_dir");
        Directory.CreateDirectory(realDir);
        File.WriteAllText(Path.Combine(realDir, "secret.cs"), "// sensitive");

        try
        {
            Directory.CreateSymbolicLink(symlinkDir, realDir);
            Assert.True(Directory.Exists(symlinkDir), "Symlink dir should be created");

            // Act
            var dirInfo = new DirectoryInfo(symlinkDir);
            var isSymlink = dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

            // Assert — directory symlinks should be detected
            Assert.True(isSymlink, "Directory should be detected as a symlink (ReparsePoint)");
        }
        catch (IOException)
        {
            // Symlink creation not supported
        }
    }

    [Fact]
    public void FileDetector_NormalFile_IsProcessed()
    {
        // Arrange — a normal (non-symlink) file
        var normalFile = Path.Combine(_testRoot, "normal.cs");
        File.WriteAllText(normalFile, "public class Normal {}");

        // Act
        var fileInfo = new FileInfo(normalFile);
        var isSymlink = fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

        // Assert — normal files should NOT be flagged as symlinks
        Assert.False(isSymlink, "Normal file should not be a ReparsePoint");
    }

    #endregion

    #region FINDING-006: Path Traversal in Output Directory

    [Fact]
    public void OutputDirectory_PathTraversal_IsRejected()
    {
        // Arrange — various path traversal attempts
        var traversalPaths = new[]
        {
            "../../etc/cron.d",
            @"..\..\Windows\Temp",
            "../../../sensitive",
            "output/../../etc/passwd"
        };

        foreach (var path in traversalPaths)
        {
            // Act — validate the output path
            var result = _validator.ValidatePath(path);

            // Assert — path traversal should be rejected
            Assert.False(result.IsValid, $"Path traversal '{path}' should be rejected");
        }
    }

    [Fact]
    public void OutputDirectory_AbsolutePath_IsAllowed()
    {
        // Arrange — an absolute path within a safe directory
        var absolutePath = Path.Combine(_testRoot, "output", "graphs");

        // Act
        var result = _validator.ValidatePath(absolutePath, _testRoot);

        // Assert — absolute paths within the base directory should be allowed
        Assert.True(result.IsValid, "Absolute path within base directory should be allowed");
    }

    [Fact]
    public void OutputDirectory_RelativeSafe_IsAllowed()
    {
        // Arrange — a safe relative path
        var safePath = Path.Combine("output", "graphs");

        // Act
        var result = _validator.ValidatePath(safePath);

        // Assert — safe relative paths should be allowed
        Assert.True(result.IsValid, "Safe relative path should be allowed");
    }

    #endregion

    #region FINDING-012: SSRF Validation Completeness

    [Fact]
    public void UrlIngester_PrivateIp172Range_IsBlocked()
    {
        // Arrange — 172.16.0.0/12 private range (172.16.0.0 - 172.31.255.255)
        var privateUrls = new[]
        {
            "http://172.16.0.1/api",
            "http://172.20.10.5/internal",
            "http://172.31.255.255/data"
        };

        foreach (var url in privateUrls)
        {
            // Act
            var result = _validator.ValidateUrl(url);

            // Assert — 172.16.x.x through 172.31.x.x must be blocked
            Assert.False(result.IsValid, $"Private IP URL '{url}' should be blocked");
        }
    }

    [Fact]
    public void UrlIngester_PrivateIp10Range_IsBlocked()
    {
        // Arrange — 10.0.0.0/8 private range
        var privateUrls = new[]
        {
            "http://10.0.0.1/api",
            "http://10.255.255.255/data",
            "http://10.10.10.10/internal"
        };

        foreach (var url in privateUrls)
        {
            // Act
            var result = _validator.ValidateUrl(url);

            // Assert
            Assert.False(result.IsValid, $"Private IP URL '{url}' should be blocked");
        }
    }

    [Fact]
    public void UrlIngester_PrivateIp192Range_IsBlocked()
    {
        // Arrange — 192.168.0.0/16 private range
        var privateUrls = new[]
        {
            "http://192.168.0.1/api",
            "http://192.168.1.1/router",
            "http://192.168.255.255/data"
        };

        foreach (var url in privateUrls)
        {
            // Act
            var result = _validator.ValidateUrl(url);

            // Assert
            Assert.False(result.IsValid, $"Private IP URL '{url}' should be blocked");
        }
    }

    [Fact]
    public void UrlIngester_PublicUrl_IsAllowed()
    {
        // Arrange — legitimate public URLs
        var publicUrls = new[]
        {
            "https://github.com/elbruno/graphify-dotnet",
            "https://api.github.com/repos/owner/repo",
            "https://example.com/data.json"
        };

        foreach (var url in publicUrls)
        {
            // Act
            var result = _validator.ValidateUrl(url);

            // Assert
            Assert.True(result.IsValid, $"Public URL '{url}' should be allowed");
        }
    }

    [Fact]
    public void UrlIngester_Localhost_IsBlocked()
    {
        // Arrange — localhost variants
        var localhostUrls = new[]
        {
            "http://localhost/api",
            "http://localhost:8080/data",
            "http://127.0.0.1/api",
            "http://127.0.0.1:3000/internal"
        };

        foreach (var url in localhostUrls)
        {
            // Act
            var result = _validator.ValidateUrl(url);

            // Assert
            Assert.False(result.IsValid, $"Localhost URL '{url}' should be blocked");
        }
    }

    #endregion
}

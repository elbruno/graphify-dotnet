using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

public sealed class FileDetectorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FileDetector _detector = new();

    public FileDetectorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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
    public async Task ExecuteAsync_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Empty(files);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testRoot, "nonexistent");
        var options = new FileDetectorOptions(RootPath: nonExistentPath, RespectGitIgnore: false);

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _detector.ExecuteAsync(options));
    }

    [Fact]
    public async Task ExecuteAsync_DiscoversSupportedFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "test.cs"), "class Test { }");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "readme.md"), "# README");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "image.png"), "fake image data");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Equal(3, files.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FiltersByExtension()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "test.cs"), "class Test { }");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "readme.md"), "# README");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "script.py"), "print('hello')");

        var options = new FileDetectorOptions(
            RootPath: _testRoot,
            IncludeExtensions: new[] { ".cs", ".py" },
            RespectGitIgnore: false
        );

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.True(f.Extension == ".cs" || f.Extension == ".py"));
    }

    [Fact]
    public async Task ExecuteAsync_RespectsMaxFileSize()
    {
        // Arrange
        var smallFile = Path.Combine(_testRoot, "small.cs");
        var largeFile = Path.Combine(_testRoot, "large.cs");

        await File.WriteAllTextAsync(smallFile, "small");
        await File.WriteAllTextAsync(largeFile, new string('A', 2000)); // 2KB

        var options = new FileDetectorOptions(
            RootPath: _testRoot,
            MaxFileSizeBytes: 1000, // 1KB limit
            RespectGitIgnore: false
        );

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal("small.cs", files[0].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_CategorizesCSharpAsCode()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "test.cs"), "class Test { }");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal(FileCategory.Code, files[0].Category);
        Assert.Equal("CSharp", files[0].Language);
    }

    [Fact]
    public async Task ExecuteAsync_CategorizesMarkdownAsDocumentation()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "readme.md"), "# README");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal(FileCategory.Documentation, files[0].Category);
        Assert.Equal("Markdown", files[0].Language);
    }

    [Fact]
    public async Task ExecuteAsync_CategorizesPngAsMedia()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "image.png"), "fake image");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal(FileCategory.Media, files[0].Category);
        Assert.Equal("PNG", files[0].Language);
    }

    [Theory]
    [InlineData(".py", "Python")]
    [InlineData(".js", "JavaScript")]
    [InlineData(".ts", "TypeScript")]
    [InlineData(".go", "Go")]
    [InlineData(".rs", "Rust")]
    [InlineData(".java", "Java")]
    public async Task ExecuteAsync_DetectsLanguageFromExtension(string extension, string expectedLanguage)
    {
        // Arrange
        var fileName = $"test{extension}";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, fileName), "code");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal(expectedLanguage, files[0].Language);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsNodeModulesDirectory()
    {
        // Arrange
        var nodeModulesDir = Path.Combine(_testRoot, "node_modules");
        Directory.CreateDirectory(nodeModulesDir);
        await File.WriteAllTextAsync(Path.Combine(nodeModulesDir, "package.js"), "module");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "app.js"), "app");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal("app.js", files[0].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsBinDirectory()
    {
        // Arrange
        var binDir = Path.Combine(_testRoot, "bin");
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(Path.Combine(binDir, "output.dll"), "binary");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "source.cs"), "code");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal("source.cs", files[0].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsHiddenFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, ".hidden.cs"), "hidden");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "visible.cs"), "visible");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal("visible.cs", files[0].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesPatterns()
    {
        // Arrange
        var testDir = Path.Combine(_testRoot, "tests");
        Directory.CreateDirectory(testDir);
        await File.WriteAllTextAsync(Path.Combine(testDir, "test.cs"), "test");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "main.cs"), "main");

        var options = new FileDetectorOptions(
            RootPath: _testRoot,
            ExcludePatterns: new[] { "tests" },
            RespectGitIgnore: false
        );

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal("main.cs", files[0].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCorrectFilePath()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "test.cs"), "code");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal(Path.Combine(_testRoot, "test.cs"), files[0].FilePath);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCorrectRelativePath()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "test.cs"), "code");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal(Path.Combine("src", "test.cs"), files[0].RelativePath);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCorrectSizeBytes()
    {
        // Arrange
        var content = "Hello, World!";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "test.cs"), content);

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.True(files[0].SizeBytes > 0);
    }

    [Fact]
    public async Task ExecuteAsync_ResultsAreSortedByRelativePath()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "zebra.cs"), "z");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "alpha.cs"), "a");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "beta.cs"), "b");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Equal(3, files.Count);
        Assert.Equal("alpha.cs", files[0].FileName);
        Assert.Equal("beta.cs", files[1].FileName);
        Assert.Equal("zebra.cs", files[2].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsUnsupportedExtensions()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "test.cs"), "code");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "data.bin"), "binary");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "archive.zip"), "archive");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Single(files);
        Assert.Equal("test.cs", files[0].FileName);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNestedDirectories()
    {
        // Arrange
        var level1 = Path.Combine(_testRoot, "level1");
        var level2 = Path.Combine(level1, "level2");
        Directory.CreateDirectory(level2);

        await File.WriteAllTextAsync(Path.Combine(_testRoot, "root.cs"), "root");
        await File.WriteAllTextAsync(Path.Combine(level1, "l1.cs"), "level1");
        await File.WriteAllTextAsync(Path.Combine(level2, "l2.cs"), "level2");

        var options = new FileDetectorOptions(RootPath: _testRoot, RespectGitIgnore: false);

        // Act
        var files = await _detector.ExecuteAsync(options);

        // Assert
        Assert.Equal(3, files.Count);
        Assert.Contains(files, f => f.FileName == "root.cs");
        Assert.Contains(files, f => f.FileName == "l1.cs");
        Assert.Contains(files, f => f.FileName == "l2.cs");
    }
}

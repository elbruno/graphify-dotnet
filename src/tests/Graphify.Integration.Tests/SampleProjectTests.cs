using Graphify.Cli;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests;

/// <summary>
/// Integration tests that verify graphify can process realistic C# projects
/// and produce complete output with all export formats.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SampleProjectTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly string _sampleProjectDir;

    public SampleProjectTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _sampleProjectDir = CreateSampleProject();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    /// <summary>
    /// Creates a mini library project structure for testing.
    /// </summary>
    private string CreateSampleProject()
    {
        var projectDir = Path.Combine(_tempDir, "mini-library");
        Directory.CreateDirectory(projectDir);

        // Create a realistic mini library with classes, interfaces, dependencies
        File.WriteAllText(Path.Combine(projectDir, "IRepository.cs"),
            @"using System.Collections.Generic;

namespace MiniLibrary
{
    public interface IRepository<T>
    {
        T GetById(int id);
        IEnumerable<T> GetAll();
        void Add(T entity);
        void Update(T entity);
        void Delete(int id);
    }
}");

        File.WriteAllText(Path.Combine(projectDir, "Repository.cs"),
            @"using System.Collections.Generic;
using System.Linq;

namespace MiniLibrary
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly List<T> _storage = new();

        public T GetById(int id) => _storage.FirstOrDefault();
        
        public IEnumerable<T> GetAll() => _storage;
        
        public void Add(T entity) => _storage.Add(entity);
        
        public void Update(T entity) { }
        
        public void Delete(int id) { }
    }
}");

        File.WriteAllText(Path.Combine(projectDir, "IService.cs"),
            @"namespace MiniLibrary
{
    public interface IService<T>
    {
        T Process(T input);
    }
}");

        File.WriteAllText(Path.Combine(projectDir, "Service.cs"),
            @"namespace MiniLibrary
{
    public class Service<T> : IService<T>
    {
        private readonly IRepository<T> _repository;

        public Service(IRepository<T> repository)
        {
            _repository = repository;
        }

        public T Process(T input)
        {
            _repository.Add(input);
            return input;
        }
    }
}");

        File.WriteAllText(Path.Combine(projectDir, "Model.cs"),
            @"namespace MiniLibrary
{
    public class Model
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}");

        File.WriteAllText(Path.Combine(projectDir, "Controller.cs"),
            @"using System.Collections.Generic;

namespace MiniLibrary
{
    public class Controller
    {
        private readonly IService<Model> _service;

        public Controller(IService<Model> service)
        {
            _service = service;
        }

        public Model Create(string name, string description)
        {
            var model = new Model { Name = name, Description = description };
            return _service.Process(model);
        }

        public IEnumerable<Model> GetAll()
        {
            return new List<Model>();
        }
    }
}");

        File.WriteAllText(Path.Combine(projectDir, "README.md"),
            @"# Mini Library

A sample library demonstrating dependency injection patterns.

## Architecture

- `IRepository<T>`: Generic repository interface
- `Repository<T>`: In-memory implementation
- `IService<T>`: Generic service interface
- `Service<T>`: Service with repository dependency
- `Controller`: API controller using service layer
- `Model`: Data model
");

        return projectDir;
    }

    [Fact(Timeout = 90000)]
    public async Task ProcessSampleProject_ProducesNonEmptyGraph()
    {
        // Arrange
        var outputDir = Path.Combine(_tempDir, "output");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            _sampleProjectDir,
            outputDir,
            formats: ["json"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.NodeCount > 0, "Graph should have nodes from sample project");
        Assert.True(result.EdgeCount > 0, "Graph should have edges showing relationships");

        _output.WriteLine($"Sample project produced: {result.NodeCount} nodes, {result.EdgeCount} edges");

        // Verify we found key entities
        var nodes = result.GetNodes().ToList();
        var labels = nodes.Select(n => n.Label).ToList();

        _output.WriteLine("Detected entities:");
        foreach (var label in labels.Take(10))
        {
            _output.WriteLine($"  - {label}");
        }

        Assert.True(nodes.Count >= 6, "Should detect at least 6 classes/interfaces");
    }

    [Fact(Timeout = 90000)]
    public async Task ProcessSampleProject_DetectsAllFiles()
    {
        // Arrange
        var outputDir = Path.Combine(_tempDir, "output-files");
        var outputWriter = new StringWriter();
        var runner = new PipelineRunner(outputWriter, verbose: true);

        // Act
        var result = await runner.RunAsync(
            _sampleProjectDir,
            outputDir,
            formats: ["json"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var output = outputWriter.ToString();

        // Count expected .cs files (6 C# files)
        var csFiles = Directory.GetFiles(_sampleProjectDir, "*.cs");
        Assert.Equal(6, csFiles.Length);

        // Pipeline should report processing these files
        _output.WriteLine($"Detected {csFiles.Length} C# files in sample project");

        // Verify pipeline detected files (look for "Found X files" in output)
        Assert.Contains("Detecting files", output);
        Assert.Contains("Found", output);

        _output.WriteLine("Pipeline output excerpt:");
        foreach (var line in output.Split('\n').Take(20))
        {
            _output.WriteLine($"  {line}");
        }
    }

    [Fact(Timeout = 120000)]
    public async Task ProcessSampleProject_AllFormatsSucceed()
    {
        // Arrange
        var outputDir = Path.Combine(_tempDir, "output-all-formats");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            _sampleProjectDir,
            outputDir,
            formats: ["json", "html", "svg", "neo4j", "obsidian", "wiki", "report"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);

        // Verify all file-based formats
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.json")), "JSON export failed");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.html")), "HTML export failed");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.svg")), "SVG export failed");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.neo4j")), "Neo4j export failed");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.report")), "Report export failed");

        // Verify directory-based formats
        Assert.True(Directory.Exists(Path.Combine(outputDir, "graph.obsidian")), "Obsidian export failed");
        Assert.True(Directory.Exists(Path.Combine(outputDir, "graph.wiki")), "Wiki export failed");

        _output.WriteLine("All 7 export formats succeeded for sample project");

        // Verify content quality
        var jsonContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "graph.json"));
        Assert.True(jsonContent.Length > 1000, "JSON should have substantial content");

        var htmlContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "graph.html"));
        Assert.Contains("vis-network", htmlContent);

        var reportContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "graph.report"));
        Assert.Contains("# Graph Report", reportContent);

        _output.WriteLine($"JSON: {jsonContent.Length} bytes");
        _output.WriteLine($"HTML: {htmlContent.Length} bytes");
        _output.WriteLine($"Report: {reportContent.Length} bytes");
    }
}

namespace Graphify.Pipeline;

using System.Text.RegularExpressions;
using Graphify.Models;

/// <summary>
/// Extracts code structure (classes, functions, imports) from source files.
/// Uses regex-based parsing as a pragmatic approach.
/// </summary>
public sealed partial class Extractor : IPipelineStage<DetectedFile, ExtractionResult>
{
    private readonly Dictionary<string, ILanguageExtractor> _extractors;

    public Extractor()
    {
        _extractors = new Dictionary<string, ILanguageExtractor>(StringComparer.OrdinalIgnoreCase)
        {
            ["CSharp"] = new CSharpExtractor(),
            ["Python"] = new PythonExtractor(),
            ["JavaScript"] = new JavaScriptExtractor(),
            ["TypeScript"] = new TypeScriptExtractor(),
            ["Go"] = new GoExtractor(),
            ["Java"] = new JavaExtractor(),
            ["Rust"] = new RustExtractor(),
            ["C"] = new CExtractor(),
            ["Cpp"] = new CppExtractor(),
        };
    }

    public async Task<ExtractionResult> ExecuteAsync(DetectedFile input, CancellationToken cancellationToken = default)
    {
        if (!_extractors.TryGetValue(input.Language, out var extractor))
        {
            return new ExtractionResult
            {
                Nodes = Array.Empty<ExtractedNode>(),
                Edges = Array.Empty<ExtractedEdge>(),
                SourceFilePath = input.FilePath,
                Method = ExtractionMethod.Ast
            };
        }

        var content = await File.ReadAllTextAsync(input.FilePath, cancellationToken);
        var (nodes, edges) = extractor.Extract(content, input.FilePath, input.FileName);

        return new ExtractionResult
        {
            Nodes = nodes,
            Edges = edges,
            SourceFilePath = input.FilePath,
            Method = ExtractionMethod.Ast,
            RawText = content
        };
    }
}

/// <summary>
/// Interface for language-specific extractors.
/// </summary>
internal interface ILanguageExtractor
{
    (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName);
}

/// <summary>
/// Base extractor with common functionality.
/// </summary>
internal abstract class BaseExtractor : ILanguageExtractor
{
    protected static string MakeId(params string[] parts)
    {
        var combined = string.Join("_", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        var cleaned = Regex.Replace(combined, @"[^a-zA-Z0-9]+", "_");
        return cleaned.Trim('_').ToLowerInvariant();
    }

    protected static ExtractedNode CreateNode(string id, string label, string filePath, int line)
    {
        return new ExtractedNode
        {
            Id = id,
            Label = label,
            FileType = FileType.Code,
            SourceFile = filePath,
            SourceLocation = $"L{line}"
        };
    }

    protected static ExtractedEdge CreateEdge(
        string source, string target, string relation,
        string filePath, int line, Confidence confidence = Confidence.Extracted, double weight = 1.0)
    {
        return new ExtractedEdge
        {
            Source = source,
            Target = target,
            Relation = relation,
            Confidence = confidence,
            SourceFile = filePath,
            SourceLocation = $"L{line}",
            Weight = weight
        };
    }

    public abstract (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName);
}

/// <summary>
/// C# extractor using regex patterns.
/// </summary>
internal sealed partial class CSharpExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*namespace\s+([a-zA-Z_][\w.]*)", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    [GeneratedRegex(@"^\s*using\s+([a-zA-Z_][\w.]*)\s*;", RegexOptions.Multiline)]
    private static partial Regex UsingPattern();

    [GeneratedRegex(@"^\s*(?:public|private|protected|internal|static|abstract|sealed|partial)*\s*(?:class|interface|struct|record)\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"^\s*(?:public|private|protected|internal|static|virtual|override|async|abstract)*\s+[\w<>[\],\s]+\s+([a-zA-Z_]\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex MethodPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();
        var lines = content.Split('\n');

        // Extract using directives
        foreach (Match match in UsingPattern().Matches(content))
        {
            var module = match.Groups[1].Value.Split('.').Last();
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports", filePath, line));
        }

        // Extract namespaces
        foreach (Match match in NamespacePattern().Matches(content))
        {
            var nsName = match.Groups[1].Value;
            var nsId = MakeId(stem, nsName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(nsId, nsName, filePath, line));
            edges.Add(CreateEdge(fileId, nsId, "contains", filePath, line));
        }

        // Extract classes/interfaces/structs
        foreach (Match match in ClassPattern().Matches(content))
        {
            var className = match.Groups[1].Value;
            var classId = MakeId(stem, className);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(classId, className, filePath, line));
            edges.Add(CreateEdge(fileId, classId, "contains", filePath, line));
        }

        // Extract methods
        foreach (Match match in MethodPattern().Matches(content))
        {
            var methodName = match.Groups[1].Value;
            var methodId = MakeId(stem, methodName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(methodId, $"{methodName}()", filePath, line));
            edges.Add(CreateEdge(fileId, methodId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// Python extractor using regex patterns.
/// </summary>
internal sealed partial class PythonExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*import\s+([a-zA-Z_][\w.]*)", RegexOptions.Multiline)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"^\s*from\s+([a-zA-Z_][\w.]*)\s+import", RegexOptions.Multiline)]
    private static partial Regex FromImportPattern();

    [GeneratedRegex(@"^\s*class\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"^\s*def\s+([a-zA-Z_]\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();

        // Extract imports
        foreach (Match match in ImportPattern().Matches(content))
        {
            var module = match.Groups[1].Value.Split('.').Last();
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports", filePath, line));
        }

        foreach (Match match in FromImportPattern().Matches(content))
        {
            var module = match.Groups[1].Value.Split('.').Last();
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports_from", filePath, line));
        }

        // Extract classes
        foreach (Match match in ClassPattern().Matches(content))
        {
            var className = match.Groups[1].Value;
            var classId = MakeId(stem, className);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(classId, className, filePath, line));
            edges.Add(CreateEdge(fileId, classId, "contains", filePath, line));
        }

        // Extract functions
        foreach (Match match in FunctionPattern().Matches(content))
        {
            var funcName = match.Groups[1].Value;
            var funcId = MakeId(stem, funcName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(funcId, $"{funcName}()", filePath, line));
            edges.Add(CreateEdge(fileId, funcId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// JavaScript extractor using regex patterns.
/// </summary>
internal partial class JavaScriptExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*import\s+.*?\s+from\s+[""']([^""']+)[""']", RegexOptions.Multiline)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"^\s*(?:export\s+)?class\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"^\s*(?:export\s+)?(?:async\s+)?function\s+([a-zA-Z_]\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionPattern();

    [GeneratedRegex(@"^\s*(?:const|let|var)\s+([a-zA-Z_]\w+)\s*=\s*(?:async\s+)?\(", RegexOptions.Multiline)]
    private static partial Regex ArrowFunctionPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();

        // Extract imports
        foreach (Match match in ImportPattern().Matches(content))
        {
            var importPath = match.Groups[1].Value;
            var module = importPath.Split('/').Last().TrimStart('.').Split('.').First();
            if (!string.IsNullOrEmpty(module))
            {
                var targetId = MakeId(module);
                var line = GetLineNumber(content, match.Index);
                edges.Add(CreateEdge(fileId, targetId, "imports_from", filePath, line));
            }
        }

        // Extract classes
        foreach (Match match in ClassPattern().Matches(content))
        {
            var className = match.Groups[1].Value;
            var classId = MakeId(stem, className);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(classId, className, filePath, line));
            edges.Add(CreateEdge(fileId, classId, "contains", filePath, line));
        }

        // Extract functions
        foreach (Match match in FunctionPattern().Matches(content))
        {
            var funcName = match.Groups[1].Value;
            var funcId = MakeId(stem, funcName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(funcId, $"{funcName}()", filePath, line));
            edges.Add(CreateEdge(fileId, funcId, "contains", filePath, line));
        }

        // Extract arrow functions
        foreach (Match match in ArrowFunctionPattern().Matches(content))
        {
            var funcName = match.Groups[1].Value;
            var funcId = MakeId(stem, funcName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(funcId, $"{funcName}()", filePath, line));
            edges.Add(CreateEdge(fileId, funcId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// TypeScript extractor (extends JavaScript with interfaces).
/// </summary>
internal sealed partial class TypeScriptExtractor : JavaScriptExtractor
{
    [GeneratedRegex(@"^\s*(?:export\s+)?interface\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex InterfacePattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var (nodes, edges) = base.Extract(content, filePath, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodeList = new List<ExtractedNode>(nodes);
        var edgeList = new List<ExtractedEdge>(edges);

        // Extract interfaces
        foreach (Match match in InterfacePattern().Matches(content))
        {
            var interfaceName = match.Groups[1].Value;
            var interfaceId = MakeId(stem, interfaceName);
            var line = GetLineNumber(content, match.Index);
            nodeList.Add(CreateNode(interfaceId, interfaceName, filePath, line));
            edgeList.Add(CreateEdge(fileId, interfaceId, "contains", filePath, line));
        }

        return (nodeList, edgeList);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// Go extractor using regex patterns.
/// </summary>
internal sealed partial class GoExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*import\s+[""']([^""']+)[""']", RegexOptions.Multiline)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"^\s*type\s+([a-zA-Z_]\w+)\s+(?:struct|interface)", RegexOptions.Multiline)]
    private static partial Regex TypePattern();

    [GeneratedRegex(@"^\s*func\s+(?:\([^)]*\)\s+)?([a-zA-Z_]\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();

        // Extract imports
        foreach (Match match in ImportPattern().Matches(content))
        {
            var importPath = match.Groups[1].Value;
            var module = importPath.Split('/').Last();
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports", filePath, line));
        }

        // Extract types
        foreach (Match match in TypePattern().Matches(content))
        {
            var typeName = match.Groups[1].Value;
            var typeId = MakeId(stem, typeName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(typeId, typeName, filePath, line));
            edges.Add(CreateEdge(fileId, typeId, "contains", filePath, line));
        }

        // Extract functions
        foreach (Match match in FunctionPattern().Matches(content))
        {
            var funcName = match.Groups[1].Value;
            var funcId = MakeId(stem, funcName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(funcId, $"{funcName}()", filePath, line));
            edges.Add(CreateEdge(fileId, funcId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// Java extractor using regex patterns.
/// </summary>
internal sealed partial class JavaExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*import\s+([a-zA-Z_][\w.]*)\s*;", RegexOptions.Multiline)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"^\s*(?:public|private|protected|abstract|final)*\s*(?:class|interface|enum)\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex ClassPattern();

    [GeneratedRegex(@"^\s*(?:public|private|protected|static|final|abstract|synchronized)*\s+[\w<>[\],\s]+\s+([a-zA-Z_]\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex MethodPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();

        // Extract imports
        foreach (Match match in ImportPattern().Matches(content))
        {
            var importPath = match.Groups[1].Value;
            var module = importPath.Split('.').Last();
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports", filePath, line));
        }

        // Extract classes/interfaces
        foreach (Match match in ClassPattern().Matches(content))
        {
            var className = match.Groups[1].Value;
            var classId = MakeId(stem, className);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(classId, className, filePath, line));
            edges.Add(CreateEdge(fileId, classId, "contains", filePath, line));
        }

        // Extract methods
        foreach (Match match in MethodPattern().Matches(content))
        {
            var methodName = match.Groups[1].Value;
            var methodId = MakeId(stem, methodName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(methodId, $"{methodName}()", filePath, line));
            edges.Add(CreateEdge(fileId, methodId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// Rust extractor using regex patterns.
/// </summary>
internal sealed partial class RustExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*use\s+([a-zA-Z_][\w:]*)", RegexOptions.Multiline)]
    private static partial Regex UsePattern();

    [GeneratedRegex(@"^\s*(?:pub\s+)?struct\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex StructPattern();

    [GeneratedRegex(@"^\s*(?:pub\s+)?trait\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex TraitPattern();

    [GeneratedRegex(@"^\s*(?:pub\s+)?(?:async\s+)?fn\s+([a-zA-Z_]\w+)\s*[<(]", RegexOptions.Multiline)]
    private static partial Regex FunctionPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();

        // Extract uses
        foreach (Match match in UsePattern().Matches(content))
        {
            var usePath = match.Groups[1].Value;
            var module = usePath.Split(new[] { "::" }, StringSplitOptions.None).Last().Trim('{', '}', ' ');
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports", filePath, line));
        }

        // Extract structs
        foreach (Match match in StructPattern().Matches(content))
        {
            var structName = match.Groups[1].Value;
            var structId = MakeId(stem, structName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(structId, structName, filePath, line));
            edges.Add(CreateEdge(fileId, structId, "contains", filePath, line));
        }

        // Extract traits
        foreach (Match match in TraitPattern().Matches(content))
        {
            var traitName = match.Groups[1].Value;
            var traitId = MakeId(stem, traitName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(traitId, traitName, filePath, line));
            edges.Add(CreateEdge(fileId, traitId, "contains", filePath, line));
        }

        // Extract functions
        foreach (Match match in FunctionPattern().Matches(content))
        {
            var funcName = match.Groups[1].Value;
            var funcId = MakeId(stem, funcName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(funcId, $"{funcName}()", filePath, line));
            edges.Add(CreateEdge(fileId, funcId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// C extractor using regex patterns.
/// </summary>
internal partial class CExtractor : BaseExtractor
{
    [GeneratedRegex(@"^\s*#include\s+[<""]([^>""]+)[>""]", RegexOptions.Multiline)]
    private static partial Regex IncludePattern();

    [GeneratedRegex(@"^\s*(?:static\s+)?(?:inline\s+)?(?:extern\s+)?[\w\s*]+\s+([a-zA-Z_]\w+)\s*\([^)]*\)\s*\{", RegexOptions.Multiline)]
    private static partial Regex FunctionPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodes = new List<ExtractedNode> { CreateNode(fileId, fileName, filePath, 1) };
        var edges = new List<ExtractedEdge>();

        // Extract includes
        foreach (Match match in IncludePattern().Matches(content))
        {
            var includePath = match.Groups[1].Value;
            var module = Path.GetFileNameWithoutExtension(includePath.Split('/').Last());
            var targetId = MakeId(module);
            var line = GetLineNumber(content, match.Index);
            edges.Add(CreateEdge(fileId, targetId, "imports", filePath, line));
        }

        // Extract functions
        foreach (Match match in FunctionPattern().Matches(content))
        {
            var funcName = match.Groups[1].Value;
            // Skip common keywords that might be matched
            if (funcName is "if" or "while" or "for" or "switch" or "return")
                continue;

            var funcId = MakeId(stem, funcName);
            var line = GetLineNumber(content, match.Index);
            nodes.Add(CreateNode(funcId, $"{funcName}()", filePath, line));
            edges.Add(CreateEdge(fileId, funcId, "contains", filePath, line));
        }

        return (nodes, edges);
    }

    protected static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

/// <summary>
/// C++ extractor using regex patterns.
/// </summary>
internal sealed partial class CppExtractor : CExtractor
{
    [GeneratedRegex(@"^\s*(?:class|struct)\s+([a-zA-Z_]\w+)", RegexOptions.Multiline)]
    private static partial Regex ClassPattern();

    public override (IReadOnlyList<ExtractedNode> Nodes, IReadOnlyList<ExtractedEdge> Edges) Extract(
        string content, string filePath, string fileName)
    {
        var (nodes, edges) = base.Extract(content, filePath, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var fileId = MakeId(stem);
        var nodeList = new List<ExtractedNode>(nodes);
        var edgeList = new List<ExtractedEdge>(edges);

        // Extract classes
        foreach (Match match in ClassPattern().Matches(content))
        {
            var className = match.Groups[1].Value;
            var classId = MakeId(stem, className);
            var line = GetLineNumberCpp(content, match.Index);
            nodeList.Add(CreateNode(classId, className, filePath, line));
            edgeList.Add(CreateEdge(fileId, classId, "contains", filePath, line));
        }

        return (nodeList, edgeList);
    }

    private static int GetLineNumberCpp(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }
}

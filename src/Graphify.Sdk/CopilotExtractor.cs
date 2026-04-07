using System.Text.Json;
using System.Text.Json.Serialization;
using Graphify.Models;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Extracts high-level semantic concepts, design patterns, and relationships from files
/// using AI providers via Microsoft.Extensions.AI IChatClient.
/// </summary>
public class CopilotExtractor : IPipelineStage<DetectedFile, ExtractionResult>
{
    private readonly IChatClient? _chatClient;
    private readonly CopilotExtractorOptions _options;

    /// <summary>
    /// Creates a new CopilotExtractor with a pre-configured IChatClient.
    /// </summary>
    /// <param name="chatClient">IChatClient configured for an AI provider.</param>
    /// <param name="options">Configuration options. If null, uses defaults.</param>
    public CopilotExtractor(IChatClient? chatClient, CopilotExtractorOptions? options = null)
    {
        _chatClient = chatClient;
        _options = options ?? new CopilotExtractorOptions();
    }

    public async Task<ExtractionResult> ExecuteAsync(DetectedFile input, CancellationToken cancellationToken = default)
    {
        // Graceful degradation: if no AI client configured, return empty results
        if (_chatClient == null)
        {
            return CreateEmptyResult(input);
        }

        // Check file size limit
        if (input.SizeBytes > _options.MaxFileSizeBytes)
        {
            return CreateEmptyResult(input);
        }

        // Decide whether to process based on file category and options
        var shouldProcess = input.Category switch
        {
            FileCategory.Code => _options.ExtractFromCode,
            FileCategory.Documentation => _options.ExtractFromDocs,
            FileCategory.Media => _options.ExtractFromMedia,
            _ => false
        };

        if (!shouldProcess)
        {
            return CreateEmptyResult(input);
        }

        try
        {
            var extractedData = await ExtractFromFileAsync(input, cancellationToken);
            return extractedData;
        }
        catch (Exception)
        {
            // On any error (API failure, malformed response, rate limits), return empty result
            // This allows the pipeline to continue even if semantic extraction fails
            return CreateEmptyResult(input);
        }
    }

    private async Task<ExtractionResult> ExtractFromFileAsync(DetectedFile file, CancellationToken cancellationToken)
    {
        // Read file content
        var fileContent = await File.ReadAllTextAsync(file.FilePath, cancellationToken);

        // Build the extraction prompt based on file category (reuse ExtractionPrompts from core library)
        var prompt = BuildPrompt(file, fileContent);

        // Create chat messages
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt)
        };

        // Set up chat options
        var chatOptions = new ChatOptions
        {
            Temperature = _options.Temperature,
            MaxOutputTokens = _options.MaxTokens,
            ModelId = _options.ModelId
        };

        // Call the LLM via AI provider
        var response = await _chatClient!.GetResponseAsync(messages, options: chatOptions, cancellationToken: cancellationToken);

        // Parse the JSON response
        var jsonResponse = response.Text ?? "{}";
        var extractionData = ParseJsonResponse(jsonResponse);

        // Convert to ExtractionResult
        return ConvertToExtractionResult(extractionData, file);
    }

    private string BuildPrompt(DetectedFile file, string fileContent)
    {
        return file.Category switch
        {
            FileCategory.Code => ExtractionPrompts.CodeSemanticExtraction(
                file.FileName,
                fileContent,
                _options.MaxNodesPerFile),

            FileCategory.Documentation => ExtractionPrompts.DocumentationExtraction(
                file.FileName,
                fileContent,
                _options.MaxNodesPerFile),

            FileCategory.Media when file.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) =>
                ExtractionPrompts.PaperExtraction(file.FileName, fileContent, _options.MaxNodesPerFile),

            FileCategory.Media => ExtractionPrompts.ImageVisionExtraction(
                file.FileName,
                _options.MaxNodesPerFile),

            _ => throw new InvalidOperationException($"Unsupported file category: {file.Category}")
        };
    }

    private ExtractionData ParseJsonResponse(string jsonResponse)
    {
        try
        {
            // Try to extract JSON from markdown code blocks if present
            var cleanJson = ExtractJsonFromMarkdown(jsonResponse);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var data = JsonSerializer.Deserialize<ExtractionData>(cleanJson, options);
            return data ?? new ExtractionData();
        }
        catch
        {
            // If parsing fails, return empty data
            return new ExtractionData();
        }
    }

    private string ExtractJsonFromMarkdown(string text)
    {
        // Remove markdown code fences if present
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```json"))
        {
            trimmed = trimmed.Substring(7);
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed.Substring(3);
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        return trimmed.Trim();
    }

    private ExtractionResult ConvertToExtractionResult(ExtractionData data, DetectedFile sourceFile)
    {
        var nodes = new List<ExtractedNode>();
        var edges = new List<ExtractedEdge>();

        // Convert nodes
        foreach (var node in data.Nodes ?? [])
        {
            var fileType = node.Type?.ToLowerInvariant() switch
            {
                "code" => FileType.Code,
                "document" => FileType.Document,
                "paper" => FileType.Paper,
                "image" => FileType.Image,
                _ => FileType.Code
            };

            var metadata = new Dictionary<string, object>();
            if (node.Metadata != null)
            {
                foreach (var kvp in node.Metadata)
                {
                    metadata[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }

            nodes.Add(new ExtractedNode
            {
                Id = node.Id ?? Guid.NewGuid().ToString(),
                Label = node.Label ?? "Unknown",
                FileType = fileType,
                SourceFile = sourceFile.FilePath,
                SourceLocation = null,
                Metadata = metadata.Count > 0 ? metadata : null
            });
        }

        // Convert edges
        foreach (var edge in data.Edges ?? [])
        {
            var confidence = edge.Confidence?.ToUpperInvariant() switch
            {
                "EXTRACTED" => Confidence.Extracted,
                "INFERRED" => Confidence.Inferred,
                "AMBIGUOUS" => Confidence.Ambiguous,
                _ => Confidence.Inferred // Default to inferred for semantic extraction
            };

            edges.Add(new ExtractedEdge
            {
                Source = edge.Source ?? string.Empty,
                Target = edge.Target ?? string.Empty,
                Relation = edge.Relation ?? "related_to",
                Confidence = confidence,
                SourceFile = sourceFile.FilePath,
                SourceLocation = null,
                Weight = edge.Weight ?? 1.0
            });
        }

        // Add confidence scores
        var confidenceScores = new Dictionary<string, double>();
        foreach (var edge in data.Edges ?? [])
        {
            if (edge.Weight.HasValue && !string.IsNullOrEmpty(edge.Source) && !string.IsNullOrEmpty(edge.Target))
            {
                confidenceScores[$"{edge.Source}->{edge.Target}"] = edge.Weight.Value;
            }
        }

        return new ExtractionResult
        {
            Nodes = nodes,
            Edges = edges,
            SourceFilePath = sourceFile.FilePath,
            Method = ExtractionMethod.Semantic,
            ConfidenceScores = confidenceScores.Count > 0 ? confidenceScores : null
        };
    }

    private ExtractionResult CreateEmptyResult(DetectedFile file)
    {
        return new ExtractionResult
        {
            Nodes = Array.Empty<ExtractedNode>(),
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = file.FilePath,
            Method = ExtractionMethod.Semantic
        };
    }

    // Internal DTOs for JSON deserialization (matching SemanticExtractor pattern)
    private class ExtractionData
    {
        public List<NodeData>? Nodes { get; set; }
        public List<EdgeData>? Edges { get; set; }
    }

    private class NodeData
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    private class EdgeData
    {
        public string? Source { get; set; }
        public string? Target { get; set; }
        public string? Relation { get; set; }
        public string? Confidence { get; set; }
        public double? Weight { get; set; }
    }
}


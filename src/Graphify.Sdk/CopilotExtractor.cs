using Graphify.Models;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Extracts high-level semantic concepts, design patterns, and relationships from files
/// using AI providers via Microsoft.Extensions.AI IChatClient.
/// </summary>
[Obsolete("Use SemanticExtractor from Graphify.Pipeline with any IChatClient (including CopilotChatClient). This class is a duplicate and will be removed in a future version.")]
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

        // Validate and sanitize LLM response before it enters the pipeline (FINDING-003)
        var jsonResponse = response.Text ?? "{}";
        var validated = LlmResponseValidator.ValidateAndSanitize(jsonResponse, file.FilePath);

        if (validated is null)
        {
            return CreateEmptyResult(file);
        }

        // Convert validated data to ExtractionResult
        return ConvertToExtractionResult(validated, file);
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

    private ExtractionResult ConvertToExtractionResult(LlmResponseValidator.LlmExtractionData data, DetectedFile sourceFile)
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
            RelativeSourceFilePath = sourceFile.RelativePath,
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
            RelativeSourceFilePath = file.RelativePath,
            Method = ExtractionMethod.Semantic
        };
    }
}
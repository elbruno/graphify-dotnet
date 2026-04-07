namespace Graphify.Sdk;

/// <summary>
/// Configuration options for CopilotExtractor.
/// </summary>
public class CopilotExtractorOptions
{
    /// <summary>
    /// API key for authentication.
    /// Required for accessing AI provider APIs.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model ID to use for extraction.
    /// Examples: "gpt-4o", "gpt-4o-mini", "llama3.2"
    /// Default: "gpt-4o"
    /// </summary>
    public string ModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// AI provider endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Maximum tokens to generate in the response.
    /// Default: 4096
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Temperature for generation (0.0 = deterministic, 1.0 = creative).
    /// Default: 0.1 (low temperature for structured extraction)
    /// </summary>
    public float Temperature { get; set; } = 0.1f;

    /// <summary>
    /// Whether to extract semantic concepts from code files.
    /// Default: true
    /// </summary>
    public bool ExtractFromCode { get; set; } = true;

    /// <summary>
    /// Whether to extract concepts from documentation files (.md, .txt, .rst).
    /// Default: true
    /// </summary>
    public bool ExtractFromDocs { get; set; } = true;

    /// <summary>
    /// Whether to extract concepts from media files (.pdf, .png, .jpg).
    /// Requires vision-capable model for images.
    /// Default: true
    /// </summary>
    public bool ExtractFromMedia { get; set; } = true;

    /// <summary>
    /// Maximum number of nodes to extract per file.
    /// Helps control token usage and response size.
    /// Default: 15
    /// </summary>
    public int MaxNodesPerFile { get; set; } = 15;

    /// <summary>
    /// Maximum file size in bytes to process.
    /// Files larger than this are skipped.
    /// Default: 1MB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 1024 * 1024;
}

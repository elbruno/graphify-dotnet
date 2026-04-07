using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Provides Microsoft Agent Framework integration for graphify's AI extraction.
/// Creates MAF agents backed by any configured IChatClient or GitHub Copilot SDK.
/// </summary>
public static class AgentFactory
{
    /// <summary>
    /// Creates a MAF ChatClientAgent backed by any IChatClient (Ollama, Azure OpenAI, etc.).
    /// The agent is configured with extraction-focused instructions.
    /// </summary>
    /// <param name="chatClient">Any IChatClient implementation.</param>
    /// <param name="instructions">System instructions for the agent. If null, uses default extraction instructions.</param>
    /// <returns>A ChatClientAgent configured for code analysis.</returns>
    public static ChatClientAgent CreateExtractionAgent(
        IChatClient chatClient,
        string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        return new ChatClientAgent(
            chatClient,
            instructions: instructions ?? DefaultExtractionInstructions,
            name: "graphify-extractor",
            description: "Extracts semantic concepts and relationships from code for knowledge graph construction.");
    }

    /// <summary>
    /// Creates a MAF AIAgent backed by GitHub Copilot SDK using the native MAF bridge.
    /// This is the recommended way to use Copilot SDK with the Agent Framework.
    /// </summary>
    /// <param name="copilotClient">An initialized CopilotClient.</param>
    /// <param name="instructions">System instructions for the agent.</param>
    /// <param name="modelId">Model to use (e.g., "gpt-4.1").</param>
    /// <returns>An AIAgent backed by GitHub Copilot.</returns>
    public static AIAgent CreateCopilotAgent(
        CopilotClient copilotClient,
        string? instructions = null,
        string modelId = "gpt-4.1")
    {
        ArgumentNullException.ThrowIfNull(copilotClient);

        return copilotClient.AsAIAgent(
            ownsClient: false,
            instructions: instructions ?? DefaultExtractionInstructions,
            name: "graphify-extractor",
            description: "Extracts semantic concepts and relationships from code for knowledge graph construction.");
    }

    private const string DefaultExtractionInstructions =
        """
        You are a code analysis expert specializing in extracting semantic concepts, 
        design patterns, and relationships from source code and documentation. 
        When analyzing code, identify:
        - Key abstractions (classes, interfaces, modules)
        - Design patterns (factory, observer, strategy, etc.)
        - Architectural layers (presentation, business logic, data access)
        - Dependencies and relationships between components
        - Cross-cutting concerns (logging, caching, authentication)
        
        Return results as structured JSON with nodes and edges.
        """;
}

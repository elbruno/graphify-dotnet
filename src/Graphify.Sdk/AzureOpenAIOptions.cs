namespace Graphify.Sdk;

/// <summary>
/// Configuration options for Azure OpenAI provider.
/// </summary>
/// <param name="Endpoint">Azure OpenAI resource endpoint, e.g. "https://myresource.openai.azure.com/"</param>
/// <param name="ApiKey">Azure OpenAI API key.</param>
/// <param name="DeploymentName">Model deployment name, e.g. "gpt-4o"</param>
/// <param name="ModelId">Optional model identifier override. Defaults to DeploymentName if null.</param>
public record AzureOpenAIOptions(
    string Endpoint,
    string ApiKey,
    string DeploymentName,
    string? ModelId = null
);

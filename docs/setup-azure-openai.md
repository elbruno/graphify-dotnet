# Using graphify-dotnet with Azure OpenAI

Harness the power of Azure OpenAI's flagship models (GPT-4o, GPT-4o-mini, GPT-4 Turbo) for semantic code analysis and AI-powered graph extraction in graphify-dotnet.

## Quick Start

1. Create an Azure OpenAI resource in [Azure Portal](https://portal.azure.com)
2. Deploy a model (e.g., `gpt-4o` or `gpt-4o-mini`)
3. Grab your endpoint and API key
4. Configure graphify-dotnet with `AzureOpenAIClientFactory` or unified `ChatClientFactory`
5. Start analyzing!

## Prerequisites

- **Azure Subscription**: [Sign up for Azure free account](https://azure.microsoft.com/en-us/free/)
- **Azure OpenAI Resource**: Access to Azure OpenAI service (request access if needed)
- **Model Deployment**: A deployed model in your Azure OpenAI resource

## Step 1: Create an Azure OpenAI Resource

### Via Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **Create a resource** → search for **"Azure OpenAI"**
3. Click **Create**
4. Fill in the form:
   - **Subscription**: Select your subscription
   - **Resource group**: Create new or select existing
   - **Region**: Choose a region (e.g., `East US`, `France Central`)
   - **Name**: e.g., `my-graphify-openai`
   - **Pricing tier**: Standard (S0)
5. Click **Review + Create** → **Create**
6. Wait for deployment to complete (2-5 minutes)

### Via Azure CLI

```bash
az cognitiveservices account create \
  --name my-graphify-openai \
  --resource-group my-resource-group \
  --kind OpenAI \
  --sku S0 \
  --location eastus
```

## Step 2: Deploy a Model

### Via Azure Portal

1. In your Azure OpenAI resource, go to **Model deployments**
2. Click **Create new deployment** → **Deploy model**
3. Select a model:
   - **gpt-4o**: Latest, most capable model (recommended for code analysis)
   - **gpt-4o-mini**: Faster, cheaper, still powerful
   - **gpt-4-turbo**: Older but stable
4. Give it a deployment name: `gpt-4o` or `gpt-4o-mini`
5. Set capacity (20 tokens/min is default for free tier)
6. Click **Create**

### Via Azure CLI

```bash
az cognitiveservices account deployment create \
  --name my-graphify-openai \
  --resource-group my-resource-group \
  --deployment-name gpt-4o \
  --model-name gpt-4o \
  --model-version "2024-08-06" \
  --model-format OpenAI \
  --scale-settings-capacity 20
```

## Step 3: Get Your Endpoint and API Key

1. In your Azure OpenAI resource, go to **Keys and Endpoint**
2. Copy:
   - **Endpoint**: e.g., `https://my-graphify-openai.openai.azure.com/`
   - **Key 1** or **Key 2**: Use either one

3. Store these securely (environment variables or secrets manager):
   ```bash
   # Linux/macOS
   export AZURE_OPENAI_ENDPOINT="https://my-graphify-openai.openai.azure.com/"
   export AZURE_OPENAI_API_KEY="your-api-key-here"
   export AZURE_OPENAI_DEPLOYMENT="gpt-4o"

   # Windows (PowerShell)
   $env:AZURE_OPENAI_ENDPOINT = "https://my-graphify-openai.openai.azure.com/"
   $env:AZURE_OPENAI_API_KEY = "your-api-key-here"
   $env:AZURE_OPENAI_DEPLOYMENT = "gpt-4o"
   ```

## Step 4: Configure graphify-dotnet

### CLI Usage (Recommended)

Use the new System.CommandLine CLI syntax to configure Azure OpenAI:

```bash
# Run with Azure OpenAI
graphify run ./my-project \
  --provider azureopenai \
  --endpoint https://myresource.openai.azure.com/ \
  --api-key sk-... \
  --deployment gpt-4o

# With custom model
graphify run ./my-project \
  --provider azureopenai \
  --endpoint https://myresource.openai.azure.com/ \
  --api-key sk-... \
  --deployment gpt-4o-mini
```

### Configuration Sources

graphify-dotnet supports a layered configuration system (priority order):
1. **CLI arguments** (highest priority)
2. **Environment variables**
3. **User secrets** (.NET user secrets)
4. **appsettings.json** (lowest priority)

### Environment Variables

Set these for automatic configuration:

```bash
# Linux/macOS
export GRAPHIFY__Provider=AzureOpenAI
export GRAPHIFY__AzureOpenAI__Endpoint=https://myresource.openai.azure.com/
export GRAPHIFY__AzureOpenAI__ApiKey=sk-...
export GRAPHIFY__AzureOpenAI__DeploymentName=gpt-4o

# Windows (PowerShell)
$env:GRAPHIFY__Provider = "AzureOpenAI"
$env:GRAPHIFY__AzureOpenAI__Endpoint = "https://myresource.openai.azure.com/"
$env:GRAPHIFY__AzureOpenAI__ApiKey = "sk-..."
$env:GRAPHIFY__AzureOpenAI__DeploymentName = "gpt-4o"
```

### User Secrets

Use .NET user secrets for local development (keeps API keys out of source):

```bash
# Set secrets for your project
dotnet user-secrets set "Graphify:Provider" "AzureOpenAI"
dotnet user-secrets set "Graphify:AzureOpenAI:Endpoint" "https://myresource.openai.azure.com/"
dotnet user-secrets set "Graphify:AzureOpenAI:ApiKey" "sk-..."
dotnet user-secrets set "Graphify:AzureOpenAI:DeploymentName" "gpt-4o"

# List configured secrets
dotnet user-secrets list
```

### appsettings.json

Configure in your application's appsettings.json (API key should still come from secrets):

```json
{
  "Graphify": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://myresource.openai.azure.com/",
      "DeploymentName": "gpt-4o",
      "ModelId": "gpt-4o"
    }
  }
}
```

### View Current Configuration

Use the `graphify config show` command to verify your configuration:

```bash
graphify config show
```

This displays the active configuration values from all sources (sensitive values like API keys are masked).

### Programmatic Configuration (Code)

For SDK usage in your own applications:

```csharp
using Graphify.Sdk;
using Microsoft.Extensions.AI;

// Use the unified ChatClientFactory
var aiOptions = new AiProviderOptions(
    Provider: AiProvider.AzureOpenAI,
    Endpoint: Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
    ApiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),
    DeploymentName: Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"),
    ModelId: "gpt-4o"
);

IChatClient client = ChatClientFactory.Create(aiOptions);

// Use the client
var response = await client.CompleteAsync("Analyze this code structure...");
Console.WriteLine(response.Message);
```

## Full Working Example

```csharp
using System;
using Graphify.Sdk;
using Microsoft.Extensions.AI;

public class CodeAnalyzer
{
    public static async Task Main(string[] args)
    {
        // 1. Create options from environment
        var options = new AiProviderOptions(
            Provider: AiProvider.AzureOpenAI,
            Endpoint: GetEnvOrThrow("AZURE_OPENAI_ENDPOINT"),
            ApiKey: GetEnvOrThrow("AZURE_OPENAI_API_KEY"),
            DeploymentName: GetEnvOrThrow("AZURE_OPENAI_DEPLOYMENT"),
            ModelId: "gpt-4o"
        );

        // 2. Create the chat client
        IChatClient client = ChatClientFactory.Create(options);

        // 3. Analyze code
        string codeSnippet = @"
public class Calculator {
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}";

        string prompt = $"Analyze this C# code and explain its structure:\n\n{codeSnippet}";
        
        var response = await client.CompleteAsync(prompt);
        Console.WriteLine("Analysis:");
        Console.WriteLine(response.Message);
    }

    private static string GetEnvOrThrow(string key)
    {
        return Environment.GetEnvironmentVariable(key)
            ?? throw new InvalidOperationException($"Missing environment variable: {key}");
    }
}
```

## Recommended Models

| Model | Use Case | Cost | Speed |
|-------|----------|------|-------|
| **gpt-4o** | Production, complex analysis | Higher | Moderate |
| **gpt-4o-mini** | Development, testing, cost-sensitive | Low | Fast |
| **gpt-4-turbo** | Legacy, large context windows | Moderate | Moderate |

## Environment Variables

Store these securely (not in source code):

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_OPENAI_ENDPOINT` | Resource endpoint | `https://my-resource.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | API key (Key 1 or Key 2) | `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx` |
| `AZURE_OPENAI_DEPLOYMENT` | Deployment name | `gpt-4o` |

## Troubleshooting

### ❌ 401 Unauthorized

**Cause**: Invalid API key or endpoint

**Solution**:
- Double-check your API key in Azure Portal → **Keys and Endpoint**
- Verify the endpoint URL matches your resource
- Ensure no trailing whitespace in credentials

```csharp
// Debug: Print (masked) credentials
Console.WriteLine($"Endpoint: {options.Endpoint}");
Console.WriteLine($"Deployment: {options.DeploymentName}");
Console.WriteLine($"Key (first 10): {options.ApiKey.Substring(0, 10)}...");
```

### ❌ Deployment Not Found

**Cause**: Deployment name doesn't exist in your resource

**Solution**:
- Go to Azure Portal → Azure OpenAI resource → **Model deployments**
- Verify the deployment name matches exactly (case-sensitive)
- Ensure the model is actually deployed (status should be "Succeeded")

```csharp
// Verify deployment exists
var deploymentName = "gpt-4o";  // Must match Azure Portal exactly
```

### ❌ Endpoint Not Found / 404

**Cause**: Invalid endpoint URL or wrong region

**Solution**:
- Copy the full endpoint from Azure Portal → **Keys and Endpoint**
- Include the trailing slash: `https://my-resource.openai.azure.com/`
- Ensure your subscription has Azure OpenAI access in that region

### ❌ Rate Limited (429 Too Many Requests)

**Cause**: Exceeded token quota

**Solution**:
- Increase deployment capacity in Azure Portal
- Add backoff/retry logic:
  ```csharp
  int retries = 3;
  while (retries-- > 0)
  {
      try
      {
          return await client.CompleteAsync(prompt);
      }
      catch (Exception ex) when (ex.Message.Contains("429") && retries > 0)
      {
          await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, 3 - retries)));
      }
  }
  ```

## Production Best Practices

1. **Use Managed Identity** (if running in Azure):
   - Replace `ApiKeyCredential` with `DefaultAzureCredential`
   - No API keys in code or environment variables

2. **Store Credentials Securely**:
   - Use Azure Key Vault for API keys
   - Use environment variables or secrets manager in CI/CD

3. **Implement Retry Logic**:
   - Handle transient failures (rate limits, timeouts)
   - Use exponential backoff

4. **Monitor Usage**:
   - Track token consumption in Azure Portal
   - Set up alerts for quota approaching

5. **Use Deployment Aliases**:
   - Deploy multiple model versions
   - Switch between versions without code changes

## See Also

- [Using graphify-dotnet with Ollama (Local Models)](./setup-ollama.md)
- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Azure OpenAI Models](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models)
- [API Reference: AzureOpenAIClientFactory](../src/Graphify.Sdk/AzureOpenAIClientFactory.cs)

## Next Steps

Once configured:
1. Run your first code analysis with `ChatClientFactory.Create(options)`
2. Explore the [README](../README.md) for full SDK capabilities
3. Check out example projects in the repository

---

**Need help?** Open an issue on [GitHub](https://github.com/BrunoCapuano/graphify-dotnet) or check the [documentation](../README.md).

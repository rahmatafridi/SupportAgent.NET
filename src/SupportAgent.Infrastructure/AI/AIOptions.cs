namespace SupportAgent.Infrastructure.AI;

/// <summary>
/// Root AI configuration bound from the <c>AI</c> section in appsettings.
/// </summary>
public class AIOptions
{
    /// <summary>Configuration section name used in appsettings and environment variables.</summary>
    public const string SectionName = "AI";

    /// <summary>Active provider name, such as Ollama or OpenAI.</summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>Settings used when the Ollama provider is selected.</summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>Settings used when the OpenAI provider is selected.</summary>
    public OpenAIOptions OpenAI { get; set; } = new();

    /// <summary>Settings used for embedding generation.</summary>
    public EmbeddingsOptions Embeddings { get; set; } = new();
}

/// <summary>
/// Ollama-specific AI settings.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// When true, Ollama is selected as the active chat provider.
    /// Only one provider should be set to true in configuration.
    /// </summary>
    public bool Use { get; set; }

    /// <summary>Base URL of the Ollama server.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Ollama model name used for chat requests.</summary>
    public string Model { get; set; } = "qwen2.5-coder:7b";
}

/// <summary>
/// OpenAI-specific AI settings.
/// </summary>
public class OpenAIOptions
{
    /// <summary>
    /// When true, OpenAI is selected as the active chat provider.
    /// Only one provider should be set to true in configuration.
    /// </summary>
    public bool Use { get; set; }

    /// <summary>OpenAI API base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>OpenAI model name used for chat requests.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>OpenAI API key used for authenticated requests.</summary>
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Embedding generation settings separate from chat completion providers.
/// </summary>
public class EmbeddingsOptions
{
    /// <summary>Embedding provider name, such as Ollama.</summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>Embedding model name, such as nomic-embed-text.</summary>
    public string Model { get; set; } = "nomic-embed-text";
}

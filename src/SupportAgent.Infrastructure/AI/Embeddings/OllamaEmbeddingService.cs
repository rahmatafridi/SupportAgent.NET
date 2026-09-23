using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;

namespace SupportAgent.Infrastructure.AI.Embeddings;

/// <summary>
/// Generates embeddings through the Ollama embedding API.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly AIOptions _options;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    /// <summary>
    /// Creates a new Ollama embedding service instance.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with the Ollama base URL.</param>
    /// <param name="options">AI configuration including the embedding model.</param>
    /// <param name="logger">Logger used for embedding diagnostics.</param>
    public OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<AIOptions> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required.", nameof(text));
        }

        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken);
        return embeddings[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        if (texts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("All texts must be non-empty.", nameof(texts));
        }

        var payload = new
        {
            model = _options.Embeddings.Model,
            input = texts
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/embed", payload, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Ollama embedding request failed with status {StatusCode}: {Body}",
                (int)response.StatusCode,
                responseBody);
            throw new InvalidOperationException(
                $"Ollama embedding request failed for model '{_options.Embeddings.Model}': {responseBody}");
        }

        var embeddingResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody);
        if (embeddingResponse?.Embeddings is null || embeddingResponse.Embeddings.Count != texts.Count)
        {
            throw new InvalidOperationException("Ollama returned an invalid embedding response.");
        }

        return embeddingResponse.Embeddings;
    }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }
}

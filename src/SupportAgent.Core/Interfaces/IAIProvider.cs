using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Represents one AI provider implementation such as Ollama or OpenAI.
/// Providers translate generic AI requests into provider-specific HTTP calls.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Gets the provider name used in configuration, such as Ollama or OpenAI.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends one AI request to the provider and returns either a normal text answer or tool calls requested by the model.
    /// </summary>
    /// <param name="request">The generic AI request.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>A generic AI response from the provider.</returns>
    Task<AIResponse> GenerateAsync(
        AIRequest request,
        CancellationToken cancellationToken = default);
}

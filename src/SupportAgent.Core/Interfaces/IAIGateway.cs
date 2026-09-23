using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Provider-independent AI gateway used by the API. Selects the configured provider and orchestrates tool calling.
/// </summary>
public interface IAIGateway
{
    /// <summary>
    /// Sends a request to the configured AI provider. When tools are supplied, runs the tool execution loop until the model returns a final answer.
    /// </summary>
    /// <param name="request">The AI request including prompt, optional conversation history, and optional tools.</param>
    /// <param name="cancellationToken">Token used to cancel provider calls and tool execution.</param>
    /// <returns>The provider response, including final text and any tools that were executed.</returns>
    Task<AIResponse> GenerateAsync(
        AIRequest request,
        CancellationToken cancellationToken = default);
}

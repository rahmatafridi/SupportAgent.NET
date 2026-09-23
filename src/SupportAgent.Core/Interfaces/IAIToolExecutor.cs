using SupportAgent.Core.Models.AI;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Executes AI tool calls selected by the LLM. Maps tool names to normal business services.
/// </summary>
public interface IAIToolExecutor
{
    /// <summary>
    /// Executes one tool call returned by the model and returns a structured result for the next model turn.
    /// </summary>
    /// <param name="toolCall">The tool call selected by the LLM.</param>
    /// <param name="cancellationToken">Token used to cancel business service calls.</param>
    /// <returns>A structured tool result containing success data or an error message.</returns>
    Task<AIToolResult> ExecuteAsync(
        AIToolCall toolCall,
        CancellationToken cancellationToken = default);
}

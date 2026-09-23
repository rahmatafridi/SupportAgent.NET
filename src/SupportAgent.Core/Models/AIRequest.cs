using SupportAgent.Core.Models.AI;

namespace SupportAgent.Core.Models;

/// <summary>
/// Request sent to the AI gateway when asking the model to generate a response.
/// </summary>
public class AIRequest
{
    /// <summary>The latest user message when no full conversation history is supplied.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Optional system instructions that define assistant behavior.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Optional prior conversation messages, including tool calls and tool results.</summary>
    public IList<AIMessage>? Messages { get; set; }

    /// <summary>Optional tools the model is allowed to call during this request.</summary>
    public IReadOnlyList<AIToolDefinition>? Tools { get; set; }
}

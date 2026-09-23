namespace SupportAgent.Core.Models.AI;

/// <summary>
/// One tool call requested by the LLM during a chat response.
/// </summary>
public class AIToolCall
{
    /// <summary>Unique identifier for this tool call in the conversation.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The tool name the model chose to invoke.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON arguments passed to the tool.</summary>
    public string ArgumentsJson { get; set; } = "{}";
}

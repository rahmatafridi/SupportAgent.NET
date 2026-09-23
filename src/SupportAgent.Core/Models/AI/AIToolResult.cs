namespace SupportAgent.Core.Models.AI;

/// <summary>
/// Result returned after executing one tool call requested by the LLM.
/// </summary>
public class AIToolResult
{
    /// <summary>The tool call ID this result corresponds to.</summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>The tool that was executed.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>JSON or text content returned to the model.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True when the tool executed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Error message when the tool failed.</summary>
    public string? Error { get; set; }
}

using SupportAgent.Core.Enums;
using SupportAgent.Core.Models.AI;

namespace SupportAgent.Core.Models;

/// <summary>
/// One message in an AI conversation, such as system, user, assistant, or tool output.
/// </summary>
public class AIMessage
{
    /// <summary>The role of the message sender.</summary>
    public AIMessageRole Role { get; set; }

    /// <summary>The message text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>When role is Tool, the ID of the tool call this message answers.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>When role is Tool, the name of the tool that produced this result.</summary>
    public string? ToolName { get; set; }

    /// <summary>When role is Assistant, any tool calls requested by the model.</summary>
    public IReadOnlyList<AIToolCall>? ToolCalls { get; set; }
}

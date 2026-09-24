namespace SupportAgent.Core.Models;

/// <summary>
/// Audit record for one tool call executed during an AI conversation.
/// </summary>
public class AIConversationToolAudit
{
    /// <summary>Unique audit record identifier.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Parent conversation identifier.</summary>
    public Guid AIConversationId { get; set; }

    /// <summary>Tool name executed by the model.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>JSON arguments passed to the tool.</summary>
    public string ArgumentsJson { get; set; } = "{}";

    /// <summary>Whether the tool execution succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>When the tool was executed.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation property for the parent conversation.</summary>
    public AIConversation Conversation { get; set; } = null!;
}

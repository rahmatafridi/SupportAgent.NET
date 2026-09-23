namespace SupportAgent.Core.Models;

/// <summary>
/// One persisted message in an AI copilot conversation.
/// </summary>
public class AIConversationMessage
{
    /// <summary>Unique message identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Parent conversation identifier.</summary>
    public Guid AIConversationId { get; set; }

    /// <summary>Message role such as user or assistant.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Message text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>When the message was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation property for the parent conversation.</summary>
    public AIConversation Conversation { get; set; } = null!;
}

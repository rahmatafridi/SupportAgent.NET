namespace SupportAgent.Core.Models;

/// <summary>
/// Persisted AI copilot conversation for a support agent session.
/// </summary>
public class AIConversation
{
    /// <summary>Unique conversation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Optional linked support ticket.</summary>
    public int? TicketId { get; set; }

    /// <summary>When the conversation was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the conversation was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Messages exchanged in this AI conversation.</summary>
    public ICollection<AIConversationMessage> Messages { get; set; } = new List<AIConversationMessage>();

    /// <summary>Tool calls recorded during this conversation.</summary>
    public ICollection<AIConversationToolAudit> ToolAudits { get; set; } = new List<AIConversationToolAudit>();
}

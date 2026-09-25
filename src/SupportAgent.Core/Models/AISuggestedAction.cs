namespace SupportAgent.Core.Models;

public enum AISuggestedActionType
{
    GetCustomerOrders,
    GetOrderStatus,
    SearchKnowledgeBase,
    ViewRecentTickets,
    LinkOrder
}

public enum AISuggestedActionStatus
{
    Pending,
    Completed,
    Failed
}

public class AISuggestedAction
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AIConversationId { get; set; }
    public int TicketId { get; set; }
    public AISuggestedActionType ActionType { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresConfirmation { get; set; }
    public AISuggestedActionStatus Status { get; set; }
    public string? TrustedArgumentsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public AIConversation Conversation { get; set; } = null!;
}

public record AISuggestedActionView(
    Guid Id,
    string Label,
    string? Description,
    AISuggestedActionType ActionType,
    bool RequiresConfirmation,
    AISuggestedActionStatus Status);

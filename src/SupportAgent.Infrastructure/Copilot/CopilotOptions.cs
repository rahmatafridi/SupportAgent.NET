namespace SupportAgent.Infrastructure.Copilot;

/// <summary>
/// Configuration for AI copilot conversation behavior.
/// </summary>
public class CopilotOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Copilot";

    /// <summary>
    /// Maximum number of persisted conversation messages sent to the model per request.
    /// </summary>
    public int MaxConversationMessages { get; set; } = 12;

    /// <summary>
    /// Maximum number of recent ticket messages included in ticket context.
    /// </summary>
    public int MaxTicketMessagesInContext { get; set; } = 10;
}

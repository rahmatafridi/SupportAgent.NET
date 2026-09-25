using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Ticket-aware AI copilot operations for support agents.
/// </summary>
public interface ICopilotService
{
    /// <summary>
    /// Answers a support agent question with ticket context and persisted conversation history.
    /// </summary>
    Task<CopilotAskResult> AskAsync(
        int ticketId,
        Guid? conversationId,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a suggested reply draft for human review. Does not send customer messages.
    /// </summary>
    Task<CopilotDraftResult> DraftReplyAsync(
        int ticketId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result returned by the copilot ask workflow.
/// </summary>
public class CopilotAskResult
{
    public AIResponse Usage { get; set; } = new();
    /// <summary>Persisted conversation identifier.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Natural-language answer for the support agent.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Tools executed while answering.</summary>
    public IReadOnlyList<AIToolCall> ToolsUsed { get; set; } = [];

    /// <summary>Grounded knowledge sources cited in the answer.</summary>
    public IReadOnlyList<KnowledgeSource> Sources { get; set; } = [];
    public IReadOnlyList<AISuggestedActionView> SuggestedActions { get; set; } = [];
    public double Confidence { get; set; }
}

/// <summary>
/// Result returned by the suggested reply workflow.
/// </summary>
public class CopilotDraftResult
{
    public AIResponse Usage { get; set; } = new();
    /// <summary>Structured suggested reply for human review.</summary>
    public CopilotDraftReply Draft { get; set; } = new();

    /// <summary>Tools executed while drafting.</summary>
    public IReadOnlyList<AIToolCall> ToolsUsed { get; set; } = [];

    /// <summary>Grounded knowledge sources cited in the draft.</summary>
    public IReadOnlyList<KnowledgeSource> Sources { get; set; } = [];
}

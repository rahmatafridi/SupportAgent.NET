using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Infrastructure.Copilot;

/// <summary>
/// Applies conversation history limits before sending messages to the AI gateway.
/// </summary>
public static class ConversationHistoryPolicy
{
    /// <summary>
    /// Returns the most recent conversation messages up to the configured limit.
    /// </summary>
    public static IReadOnlyList<AIConversationMessage> ApplyLimit(
        IEnumerable<AIConversationMessage> messages,
        CopilotOptions options)
    {
        return messages
            .OrderBy(message => message.CreatedAt)
            .TakeLast(options.MaxConversationMessages)
            .ToList();
    }
}

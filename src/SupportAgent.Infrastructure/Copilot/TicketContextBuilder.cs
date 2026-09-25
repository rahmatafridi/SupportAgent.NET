using System.Text;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Copilot;

/// <summary>
/// Builds compact ticket context for copilot prompts without dumping the full database.
/// </summary>
public static class TicketContextBuilder
{
    /// <summary>
    /// Builds a concise ticket context block for the system prompt.
    /// </summary>
    public static string Build(Ticket ticket, IReadOnlyList<TicketMessage> messages, int maxMessages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Current ticket context (use this as the primary description of the support issue):");
        builder.AppendLine($"- TicketId: {ticket.Id}");
        builder.AppendLine($"- Subject: {ticket.Subject}");
        builder.AppendLine($"- CustomerId: {ticket.CustomerId}");
        builder.AppendLine($"- RelatedOrderId: {(ticket.OrderId.HasValue ? ticket.OrderId.Value : "None")}");
        builder.AppendLine($"- Status: {ticket.Status}");
        builder.AppendLine($"- Priority: {ticket.Priority}");
        builder.AppendLine("Recent ticket messages:");

        var recentMessages = messages
            .OrderBy(message => message.CreatedAt)
            .TakeLast(maxMessages);

        foreach (var message in recentMessages)
        {
            builder.AppendLine($"- [{message.SenderType}] {message.Message}");
        }

        return builder.ToString().Trim();
    }
}

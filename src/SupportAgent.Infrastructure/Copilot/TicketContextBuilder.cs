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
        builder.AppendLine("Ticket context:");
        builder.AppendLine($"- Ticket ID: {ticket.Id}");
        builder.AppendLine($"- Subject: {ticket.Subject}");
        builder.AppendLine($"- Status: {ticket.Status}");
        builder.AppendLine($"- Priority: {ticket.Priority}");
        builder.AppendLine($"- Customer ID: {ticket.CustomerId}");
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

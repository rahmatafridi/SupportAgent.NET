using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Tests;

public class ConversationHistoryPolicyTests
{
    [Fact]
    public void ApplyLimit_KeepsMostRecentMessagesUpToConfiguredMaximum()
    {
        var options = new CopilotOptions { MaxConversationMessages = 3 };
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var messages = Enumerable.Range(1, 8)
            .Select(index => new AIConversationMessage
            {
                Id = Guid.NewGuid(),
                AIConversationId = Guid.NewGuid(),
                Role = index % 2 == 0 ? "assistant" : "user",
                Content = $"Message {index}",
                CreatedAt = createdAt.AddMinutes(index)
            })
            .ToList();

        var limited = ConversationHistoryPolicy.ApplyLimit(messages, options);

        Assert.Equal(3, limited.Count);
        Assert.Equal("Message 6", limited[0].Content);
        Assert.Equal("Message 8", limited[2].Content);
    }
}

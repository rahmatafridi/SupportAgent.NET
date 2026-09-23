using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class AIConversationServiceTests
{
    [Fact]
    public async Task CreateConversationAsync_PersistsConversationWithTicketId()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(CreateConversationAsync_PersistsConversationWithTicketId));
        var service = new AIConversationService(context);

        var conversation = await service.CreateConversationAsync(1001);

        Assert.NotEqual(Guid.Empty, conversation.Id);
        Assert.Equal(1001, conversation.TicketId);
        Assert.Equal(1, context.AIConversations.Count());
    }

    [Fact]
    public async Task AddMessageAsync_PersistsMessageAndUpdatesConversation()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AddMessageAsync_PersistsMessageAndUpdatesConversation));
        var service = new AIConversationService(context);
        var conversation = await service.CreateConversationAsync(1001);

        var message = await service.AddMessageAsync(conversation.Id, "user", "Who is customer 101?");

        Assert.Equal("user", message.Role);
        Assert.Equal("Who is customer 101?", message.Content);
        Assert.Equal(1, context.AIConversationMessages.Count());

        var updatedConversation = await service.GetConversationAsync(conversation.Id);
        Assert.NotNull(updatedConversation);
        Assert.True(updatedConversation!.UpdatedAt >= conversation.CreatedAt);
    }

    [Fact]
    public async Task GetRecentMessagesAsync_ReturnsMessagesInChronologicalOrder()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(GetRecentMessagesAsync_ReturnsMessagesInChronologicalOrder));
        var service = new AIConversationService(context);
        var conversation = await service.CreateConversationAsync(1001);

        await service.AddMessageAsync(conversation.Id, "user", "First question");
        await service.AddMessageAsync(conversation.Id, "assistant", "First answer");
        await service.AddMessageAsync(conversation.Id, "user", "Follow-up question");

        var messages = await service.GetRecentMessagesAsync(conversation.Id, 12);

        Assert.Equal(3, messages.Count);
        Assert.Equal("First question", messages[0].Content);
        Assert.Equal("Follow-up question", messages[2].Content);
    }

    [Fact]
    public async Task AddToolAuditsAsync_PersistsToolAuditEntries()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AddToolAuditsAsync_PersistsToolAuditEntries));
        var service = new AIConversationService(context);
        var conversation = await service.CreateConversationAsync(1001);

        await service.AddToolAuditsAsync(conversation.Id,
        [
            new AIConversationToolAudit
            {
                ToolName = "GetCustomer",
                ArgumentsJson = "{\"customerId\":101}",
                Success = true,
                CreatedAt = DateTime.UtcNow
            }
        ]);

        Assert.Single(context.AIConversationToolAudits);
        Assert.Equal("GetCustomer", context.AIConversationToolAudits.Single().ToolName);
    }

    [Fact]
    public async Task AddMessageAsync_Throws_WhenConversationDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AddMessageAsync_Throws_WhenConversationDoesNotExist));
        var service = new AIConversationService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddMessageAsync(Guid.NewGuid(), "user", "Hello"));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }
}

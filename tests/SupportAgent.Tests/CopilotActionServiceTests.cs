using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class CopilotActionServiceTests
{
    [Fact]
    public async Task GetCustomerOrders_UsesTrustedTicketCustomer_ContinuesConversation_AndOffersConfirmedLinks()
    {
        await using var db = TestDbContextFactory.CreateContext(nameof(GetCustomerOrders_UsesTrustedTicketCustomer_ContinuesConversation_AndOffersConfirmedLinks));
        await TestDbContextFactory.SeedSampleDataAsync(db);
        (await db.Tickets.FindAsync(1001))!.OrderId = null;
        await db.SaveChangesAsync();
        var conversations = new AIConversationService(db);
        var conversation = await conversations.CreateConversationAsync(1001);
        var action = AddAction(db, conversation.Id, AISuggestedActionType.GetCustomerOrders, "Check customer's recent orders");
        await db.SaveChangesAsync();
        var copilot = new RecordingCopilotService();
        var service = CreateService(db, conversations, copilot);

        var result = await service.ExecuteAsync(action.Id, 1001, conversation.Id, false);

        Assert.True(copilot.WasCalled);
        Assert.Equal(AISuggestedActionStatus.Completed, (await db.AISuggestedActions.FindAsync(action.Id))!.Status);
        Assert.Contains(await db.AIConversationMessages.ToListAsync(), message => message.Content.Contains("ORD-1001"));
        Assert.Equal("GetCustomerOrders", result.Action);
        Assert.Equal("Check customer's recent orders", result.ActionLabel);
        Assert.True(result.Success);
        Assert.Contains("ORD-1001", result.ResultJson);
        Assert.Contains("linkActionId", result.ResultJson);
        var links = result.UpdatedResponse.SuggestedActions.Where(item => item.ActionType == AISuggestedActionType.LinkOrder).ToList();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.True(link.RequiresConfirmation));
        Assert.Null((await db.Tickets.FindAsync(1001))!.OrderId);
    }

    [Fact]
    public async Task LinkOrder_RequiresConfirmation_AndValidatesOwnership()
    {
        await using var db = TestDbContextFactory.CreateContext(nameof(LinkOrder_RequiresConfirmation_AndValidatesOwnership));
        await TestDbContextFactory.SeedSampleDataAsync(db);
        var conversations = new AIConversationService(db);
        var conversation = await conversations.CreateConversationAsync(1001);
        var action = AddAction(db, conversation.Id, AISuggestedActionType.LinkOrder, "Link ORD-1001", true, "{\"orderId\":1}");
        await db.SaveChangesAsync();
        var service = CreateService(db, conversations, new RecordingCopilotService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(action.Id, 1001, conversation.Id, false));
        var result = await service.ExecuteAsync(action.Id, 1001, conversation.Id, true);
        Assert.Equal(1, (await db.Tickets.FindAsync(1001))!.OrderId);
        var statusAction = Assert.Single(result.UpdatedResponse.SuggestedActions,
            item => item.ActionType == AISuggestedActionType.GetOrderStatus);
        Assert.Equal("Check current order status", statusAction.Label);
        Assert.False(statusAction.RequiresConfirmation);
    }

    [Fact]
    public async Task UnknownOrMismatchedActionCannotBeInjected()
    {
        await using var db = TestDbContextFactory.CreateContext(nameof(UnknownOrMismatchedActionCannotBeInjected));
        await TestDbContextFactory.SeedSampleDataAsync(db);
        var conversations = new AIConversationService(db);
        var conversation = await conversations.CreateConversationAsync(1001);
        var service = CreateService(db, conversations, new RecordingCopilotService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(Guid.NewGuid(), 1001, conversation.Id, true));
    }

    private static AISuggestedAction AddAction(SupportAgent.Infrastructure.Data.SupportAgentDbContext db, Guid conversationId,
        AISuggestedActionType type, string label, bool confirmation = false, string trustedArguments = "{}")
    {
        var action = new AISuggestedAction { Id = Guid.NewGuid(), AIConversationId = conversationId, TicketId = 1001,
            ActionType = type, Label = label, RequiresConfirmation = confirmation, Status = AISuggestedActionStatus.Pending,
            TrustedArgumentsJson = trustedArguments, CreatedAt = DateTime.UtcNow };
        db.AISuggestedActions.Add(action); return action;
    }

    private static CopilotActionService CreateService(SupportAgent.Infrastructure.Data.SupportAgentDbContext db,
        IAIConversationService conversations, ICopilotService copilot) =>
        new(db, new OrderService(db), new EmptyKnowledgeService(), new TicketService(db), conversations, copilot);

    private sealed class RecordingCopilotService : ICopilotService
    {
        public bool WasCalled { get; private set; }
        public Task<CopilotAskResult> AskAsync(int ticketId, Guid? conversationId, string message, CancellationToken cancellationToken = default)
        { WasCalled = true; return Task.FromResult(new CopilotAskResult { ConversationId = conversationId!.Value, Answer = "Updated analysis", Usage = new AIResponse() }); }
        public Task<CopilotDraftResult> DraftReplyAsync(int ticketId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class EmptyKnowledgeService : IKnowledgeService
    {
        public Task<int> ProcessDocumentAsync(int documentId, string content, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<KnowledgeDocument> AddDocumentAsync(string title, string content, string? source = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>([]);
        public Task<int> GenerateMissingEmbeddingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}

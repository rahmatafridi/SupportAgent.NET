using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Copilot;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class CopilotServiceTests
{
    [Fact]
    public async Task AskAsync_CreatesConversationAndPersistsMessages()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_CreatesConversationAndPersistsMessages));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = "Customer 101 asked about order status.",
            Provider = "Fake",
            Model = "fake-model"
        });

        var service = CreateService(context, gateway);
        var result = await service.AskAsync(1001, null, "What is happening with this customer?");

        Assert.NotEqual(Guid.Empty, result.ConversationId);
        Assert.Equal("Customer 101 asked about order status.", result.Answer);
        Assert.Equal(2, context.AIConversationMessages.Count(message =>
            message.AIConversationId == result.ConversationId));
    }

    [Fact]
    public async Task AskAsync_ReusesSameConversationForFollowUpQuestion()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_ReusesSameConversationForFollowUpQuestion));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = "Answer",
            Provider = "Fake",
            Model = "fake-model"
        });

        var service = CreateService(context, gateway);
        var first = await service.AskAsync(1001, null, "Who is customer 101?");
        var second = await service.AskAsync(1001, first.ConversationId, "What orders do they have?");

        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.Equal(4, context.AIConversationMessages.Count(message =>
            message.AIConversationId == first.ConversationId));
    }

    [Fact]
    public async Task AskAsync_IncludesTicketContextInSystemPrompt()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_IncludesTicketContextInSystemPrompt));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        AIRequest? capturedRequest = null;
        var gateway = new RecordingAIGateway(request =>
        {
            capturedRequest = request;
            return new AIResponse
            {
                Text = "Ticket reviewed.",
                Provider = "Fake",
                Model = "fake-model"
            };
        });

        var service = CreateService(context, gateway);
        await service.AskAsync(1001, null, "Summarize this ticket.");

        Assert.NotNull(capturedRequest);
        Assert.Contains("TicketId: 1001", capturedRequest!.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Subject: Where is my order?", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Status: Open", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Priority: High", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("CustomerId: 101", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("RelatedOrderId: 2", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("[Customer] Where is my order?", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("[Agent] Checking now.", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(SupportAgentCopilotPrompts.AskAssistant, capturedRequest.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskAsync_ReturnsIssueFocusedAnswerAfterOrderStatusToolCall()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_ReturnsIssueFocusedAnswerAfterOrderStatusToolCall));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = """
                {
                  "answer": "John Smith is asking where his missing order is. Ticket #1001 is open with high priority, and order ORD-1001 is currently Processing. Verify fulfillment progress and update the customer.",
                  "suggestedActions": ["Verify fulfillment progress", "Update the customer"],
                  "confidence": 0.92
                }
                """,
            Provider = "Fake",
            Model = "fake-model",
            ToolsUsed =
            [
                new AIToolCall
                {
                    Name = SupportToolDefinitions.GetOrderStatusToolName,
                    ArgumentsJson = "{\"orderId\":2}"
                }
            ],
            ToolExecutions =
            [
                new AIToolExecutionRecord
                {
                    Name = SupportToolDefinitions.GetOrderStatusToolName,
                    ArgumentsJson = "{\"orderId\":2}",
                    Success = true,
                    Timestamp = DateTime.UtcNow
                }
            ]
        });

        var service = CreateService(context, gateway);
        var result = await service.AskAsync(1001, null, "What is happening with this customer?");

        Assert.Contains("order", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ticket #1001", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Processing", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Verify fulfillment", result.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"answer\"", result.Answer, StringComparison.Ordinal);
        Assert.Equal(0.92, result.Confidence);
        Assert.Single(result.ToolsUsed);
        Assert.Equal(SupportToolDefinitions.GetOrderStatusToolName, result.ToolsUsed[0].Name);
        Assert.Single(context.AIConversationToolAudits);
    }

    [Fact]
    public void AskAssistantPrompt_PrioritizesTicketIssueOverCustomerIdentity()
    {
        Assert.Contains("primary description of what is happening", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("cannot investigate an order", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("use GetOrderStatus", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("Use SearchKnowledgeBase", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("The order status has not yet been verified", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("Never infer that an order is", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("never guess an order ID", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("Identify or link the customer's affected order", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("order status cannot be verified yet", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
        Assert.Contains("Never return 1.0 when a required fact is missing", SupportAgentCopilotPrompts.AskAssistant,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SupportTools_PresentOrderLookupBeforeCustomerLookupWithoutRemovingEitherTool()
    {
        var tools = SupportToolDefinitions.GetAll();

        Assert.Equal(SupportToolDefinitions.GetOrderStatusToolName, tools[0].Name);
        Assert.Equal(SupportToolDefinitions.GetCustomerToolName, tools[1].Name);
        Assert.Contains("RelatedOrderId", tools[0].Description, StringComparison.Ordinal);
        Assert.Contains("cannot provide order status", tools[1].Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_WithNoLinkedOrder_DoesNotGuessOrderAndReturnsUsefulAction()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_WithNoLinkedOrder_DoesNotGuessOrderAndReturnsUsefulAction));
        await TestDbContextFactory.SeedSampleDataAsync(context);
        var ticket = await context.Tickets.SingleAsync(item => item.Id == 1001);
        ticket.OrderId = null;
        await context.SaveChangesAsync();

        AIRequest? capturedRequest = null;
        var gateway = new RecordingAIGateway(request =>
        {
            capturedRequest = request;
            return new AIResponse
            {
                Text = """
                    {
                      "answer": "John Smith is reporting that an order is missing, but this ticket is not currently linked to a specific order. The order must be identified before its status can be verified.",
                      "suggestedActions": [{"label":"GetCustomerOrders","description":"Retrieve recent orders for this ticket customer.","actionType":"GetCustomerOrders"}],
                      "confidence": 0.55
                    }
                    """,
                Provider = "Fake",
                Model = "fake-model"
            };
        });

        var service = CreateService(context, gateway);
        var result = await service.AskAsync(1001, null, "What is happening with this customer?");

        Assert.NotNull(capturedRequest);
        Assert.Contains("RelatedOrderId: None", capturedRequest!.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(capturedRequest.Tools, tool =>
            tool.Name == SupportToolDefinitions.GetOrderStatusToolName);
        Assert.Empty(result.ToolsUsed);
        Assert.Contains("not currently linked to a specific order", result.Answer, StringComparison.Ordinal);
        Assert.Equal("Check customer's recent orders", Assert.Single(result.SuggestedActions).Label);
        Assert.Equal(AISuggestedActionType.GetCustomerOrders, result.SuggestedActions[0].ActionType);
        Assert.Equal(0.55, result.Confidence);
        Assert.True(result.Confidence < 1.0);
    }

    [Fact]
    public async Task AskAsync_WithNoLinkedOrder_AddsSafeDiscoveryAction_WhenModelOmitsSuggestions()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_WithNoLinkedOrder_AddsSafeDiscoveryAction_WhenModelOmitsSuggestions));
        await TestDbContextFactory.SeedSampleDataAsync(context);
        var ticket = await context.Tickets.SingleAsync(item => item.Id == 1001);
        ticket.OrderId = null;
        await context.SaveChangesAsync();

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = """
                {
                  "answer": "This ticket is not linked to a specific order, so the order status cannot be verified yet.",
                  "suggestedActions": [],
                  "confidence": 0.6
                }
                """,
            Provider = "Fake",
            Model = "fake-model"
        });

        var result = await CreateService(context, gateway)
            .AskAsync(1001, null, "What is happening with this customer?");

        var action = Assert.Single(result.SuggestedActions);
        Assert.Equal(AISuggestedActionType.GetCustomerOrders, action.ActionType);
        Assert.Equal("Check customer's recent orders", action.Label);
        Assert.False(action.RequiresConfirmation);
        Assert.Null((await context.Tickets.FindAsync(1001))!.OrderId);
    }

    [Fact]
    public async Task AskAsync_DoesNotSuggestAnExecutedActionAgain()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_DoesNotSuggestAnExecutedActionAgain));
        await TestDbContextFactory.SeedSampleDataAsync(context);
        var conversationService = new AIConversationService(context);
        var conversation = await conversationService.CreateConversationAsync(1001);
        context.AISuggestedActions.Add(new AISuggestedAction
        {
            Id = Guid.NewGuid(),
            AIConversationId = conversation.Id,
            TicketId = 1001,
            ActionType = AISuggestedActionType.GetOrderStatus,
            Label = "Check current order status",
            Status = AISuggestedActionStatus.Completed,
            ExecutedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            TrustedArgumentsJson = "{\"orderId\":2}"
        });
        await context.SaveChangesAsync();

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = """
                {
                  "answer": "Order ORD-1002 is shipped.",
                  "suggestedActions": [{"label":"Check again","actionType":"GetOrderStatus"}],
                  "confidence": 0.9
                }
                """,
            Provider = "Fake",
            Model = "fake-model"
        });

        var result = await CreateService(context, gateway)
            .AskAsync(1001, conversation.Id, "Update the analysis.");

        Assert.DoesNotContain(result.SuggestedActions,
            action => action.ActionType == AISuggestedActionType.GetOrderStatus);
    }

    [Fact]
    public async Task AskAsync_StillAllowsToolCallingThroughGateway()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_StillAllowsToolCallingThroughGateway));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        AIRequest? capturedRequest = null;
        var gateway = new RecordingAIGateway(request =>
        {
            capturedRequest = request;
            return new AIResponse
            {
                Text = "Customer 101 is John Smith.",
                Provider = "Fake",
                Model = "fake-model",
                ToolsUsed =
                [
                    new AIToolCall
                    {
                        Name = SupportToolDefinitions.GetCustomerToolName,
                        ArgumentsJson = "{\"customerId\":101}"
                    }
                ],
                ToolExecutions =
                [
                    new AIToolExecutionRecord
                    {
                        Name = SupportToolDefinitions.GetCustomerToolName,
                        ArgumentsJson = "{\"customerId\":101}",
                        Success = true,
                        Timestamp = DateTime.UtcNow
                    }
                ]
            };
        });

        var service = CreateService(context, gateway);
        var result = await service.AskAsync(1001, null, "Who is customer 101?");

        Assert.NotNull(capturedRequest);
        Assert.Equal(SupportToolDefinitions.GetAll().Count, capturedRequest!.Tools.Count);
        Assert.Single(result.ToolsUsed);
        Assert.Equal(SupportToolDefinitions.GetCustomerToolName, result.ToolsUsed[0].Name);
        Assert.Single(context.AIConversationToolAudits);
    }

    [Fact]
    public async Task AskAsync_ReturnsSourcesAndToolsUsed()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_ReturnsSourcesAndToolsUsed));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = "Customers may request a refund within 30 days.",
            Provider = "Fake",
            Model = "fake-model",
            ToolsUsed =
            [
                new AIToolCall
                {
                    Name = SupportToolDefinitions.SearchKnowledgeBaseToolName,
                    ArgumentsJson = "{\"query\":\"refund policy\"}"
                }
            ],
            Sources =
            [
                new KnowledgeSource
                {
                    Title = "Refund Policy",
                    Source = "internal-policy"
                }
            ]
        });

        var service = CreateService(context, gateway);
        var result = await service.AskAsync(1001, null, "What is our refund policy?");

        Assert.Single(result.ToolsUsed);
        Assert.Single(result.Sources);
        Assert.Equal("Refund Policy", result.Sources[0].Title);
    }

    [Fact]
    public async Task AskAsync_LimitsConversationHistorySentToGateway()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_LimitsConversationHistorySentToGateway));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var conversationService = new AIConversationService(context);
        var conversation = await conversationService.CreateConversationAsync(1001);

        for (var index = 1; index <= 14; index++)
        {
            await conversationService.AddMessageAsync(
                conversation.Id,
                index % 2 == 0 ? "assistant" : "user",
                $"Message {index}");
        }

        AIRequest? capturedRequest = null;
        var gateway = new RecordingAIGateway(request =>
        {
            capturedRequest = request;
            return new AIResponse
            {
                Text = "Limited history applied.",
                Provider = "Fake",
                Model = "fake-model"
            };
        });

        var service = CreateService(
            context,
            gateway,
            new CopilotOptions { MaxConversationMessages = 4 });

        await service.AskAsync(1001, conversation.Id, "Another follow-up");

        Assert.NotNull(capturedRequest);
        Assert.Equal(4, capturedRequest!.Messages.Count);
        Assert.Equal("Message 12", capturedRequest.Messages[0].Content);
        Assert.Equal("Another follow-up", capturedRequest.Messages[^1].Content);
    }

    [Fact]
    public async Task AskAsync_Throws_WhenConversationIdIsUnknown()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(AskAsync_Throws_WhenConversationIdIsUnknown));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var service = CreateService(context, new RecordingAIGateway(_ => new AIResponse
        {
            Text = "unused",
            Provider = "Fake",
            Model = "fake-model"
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AskAsync(1001, Guid.NewGuid(), "Hello"));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftReplyAsync_GeneratesStructuredDraft()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(DraftReplyAsync_GeneratesStructuredDraft));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = """
                {
                  "subject": "Update on your order",
                  "body": "Thanks for contacting us. I am checking your order now.",
                  "tone": "professional",
                  "confidence": 0.9
                }
                """,
            Provider = "Fake",
            Model = "fake-model"
        });

        var service = CreateService(context, gateway);
        var result = await service.DraftReplyAsync(1001);

        Assert.Equal("Update on your order", result.Draft.Subject);
        Assert.Contains("checking your order", result.Draft.Body, StringComparison.Ordinal);
        Assert.Equal(0.9, result.Draft.Confidence);
    }

    [Fact]
    public async Task DraftReplyAsync_DoesNotSendReplyOrPersistTicketMessages()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(DraftReplyAsync_DoesNotSendReplyOrPersistTicketMessages));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var gateway = new RecordingAIGateway(_ => new AIResponse
        {
            Text = """
                {
                  "subject": "We are reviewing your order",
                  "body": "Thanks for your patience.",
                  "tone": "professional",
                  "confidence": 0.8
                }
                """,
            Provider = "Fake",
            Model = "fake-model"
        });

        var service = CreateService(context, gateway);
        var ticketMessageCountBefore = await context.TicketMessages.CountAsync();
        var aiConversationCountBefore = await context.AIConversations.CountAsync();

        await service.DraftReplyAsync(1001);

        Assert.Equal(ticketMessageCountBefore, await context.TicketMessages.CountAsync());
        Assert.Equal(aiConversationCountBefore, await context.AIConversations.CountAsync());
    }

    [Fact]
    public async Task DraftReplyAsync_IncludesTicketContextAndTools()
    {
        await using var context = TestDbContextFactory.CreateContext(
            nameof(DraftReplyAsync_IncludesTicketContextAndTools));
        await TestDbContextFactory.SeedSampleDataAsync(context);
        var conversationService = new AIConversationService(context);
        var conversation = await conversationService.CreateConversationAsync(1001);
        await conversationService.AddMessageAsync(
            conversation.Id,
            "user",
            "Trusted application action result: Order ORD-1002 has verified status Shipped.");

        AIRequest? capturedRequest = null;
        var gateway = new RecordingAIGateway(request =>
        {
            capturedRequest = request;
            return new AIResponse
            {
                Text = """
                    {
                      "subject": "Order update",
                      "body": "I checked your order status.",
                      "tone": "professional",
                      "confidence": 0.75
                    }
                    """,
                Provider = "Fake",
                Model = "fake-model",
                ToolsUsed =
                [
                    new AIToolCall
                    {
                        Name = SupportToolDefinitions.GetOrderStatusToolName,
                        ArgumentsJson = "{\"orderId\":1}"
                    }
                ]
            };
        });

        var service = CreateService(context, gateway);
        var result = await service.DraftReplyAsync(1001);

        Assert.NotNull(capturedRequest);
        Assert.Contains("TicketId: 1001", capturedRequest!.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(capturedRequest.Messages!, message =>
            message.Content.Contains("verified status Shipped", StringComparison.Ordinal));
        Assert.Contains("Never expose tool names", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Equal(SupportToolDefinitions.GetAll().Count, capturedRequest.Tools.Count);
        Assert.Single(result.ToolsUsed);
    }

    private static CopilotService CreateService(
        SupportAgent.Infrastructure.Data.SupportAgentDbContext context,
        IAIGateway gateway,
        CopilotOptions? options = null)
    {
        return new CopilotService(
            new TicketService(context),
            new AIConversationService(context),
            gateway,
            Options.Create(options ?? new CopilotOptions { MaxConversationMessages = 12 }),
            NullLogger<CopilotService>.Instance,
            new TestHostEnvironment(),
            context);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SupportAgent.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingAIGateway(Func<AIRequest, AIResponse> responseFactory) : IAIGateway
    {
        public Task<AIResponse> GenerateAsync(
            AIRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responseFactory(request));
    }
}

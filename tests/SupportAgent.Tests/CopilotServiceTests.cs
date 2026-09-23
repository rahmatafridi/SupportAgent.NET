using Microsoft.EntityFrameworkCore;
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
        Assert.Contains("Ticket ID: 1001", capturedRequest!.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Where is my order?", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Customer ID: 101", capturedRequest.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(SupportAgentCopilotPrompts.AskAssistant, capturedRequest.SystemPrompt, StringComparison.Ordinal);
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
        Assert.Contains("Ticket ID: 1001", capturedRequest!.SystemPrompt, StringComparison.Ordinal);
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
            Options.Create(options ?? new CopilotOptions { MaxConversationMessages = 12 }));
    }

    private sealed class RecordingAIGateway(Func<AIRequest, AIResponse> responseFactory) : IAIGateway
    {
        public Task<AIResponse> GenerateAsync(
            AIRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responseFactory(request));
    }
}

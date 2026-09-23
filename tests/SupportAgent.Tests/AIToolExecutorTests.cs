using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class AIToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsCustomer_WhenCustomerExists()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsCustomer_WhenCustomerExists));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-1",
            Name = SupportToolDefinitions.GetCustomerToolName,
            ArgumentsJson = "{\"customerId\":101}"
        });

        Assert.True(result.Success);
        Assert.Contains("John", result.Content, StringComparison.Ordinal);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOrderStatus_WhenOrderExists()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsOrderStatus_WhenOrderExists));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-2",
            Name = SupportToolDefinitions.GetOrderStatusToolName,
            ArgumentsJson = "{\"orderId\":1}"
        });

        Assert.True(result.Success);
        Assert.Contains("ORD-1001", result.Content, StringComparison.Ordinal);
        Assert.Contains("Processing", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredError_WhenCustomerNotFound()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsStructuredError_WhenCustomerNotFound));

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-3",
            Name = SupportToolDefinitions.GetCustomerToolName,
            ArgumentsJson = "{\"customerId\":999}"
        });

        Assert.False(result.Success);
        Assert.Equal("Customer 999 was not found.", result.Error);
        Assert.Contains("\"success\":false", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredError_WhenOrderNotFound()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsStructuredError_WhenOrderNotFound));

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-4",
            Name = SupportToolDefinitions.GetOrderStatusToolName,
            ArgumentsJson = "{\"orderId\":999}"
        });

        Assert.False(result.Success);
        Assert.Equal("Order 999 was not found.", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredError_WhenArgumentsAreInvalid()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsStructuredError_WhenArgumentsAreInvalid));

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-5",
            Name = SupportToolDefinitions.GetCustomerToolName,
            ArgumentsJson = "{\"customerId\":\"abc\"}"
        });

        Assert.False(result.Success);
        Assert.Contains("customerId", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnknownToolName()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_RejectsUnknownToolName));

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-6",
            Name = "DeleteDatabase",
            ArgumentsJson = "{}"
        });

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsKnowledgeResults_WhenSearchMatches()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsKnowledgeResults_WhenSearchMatches));
        var knowledgeService = KnowledgeServiceTestFactory.Create(context);
        await knowledgeService.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase if the order has not been fully consumed.",
            "internal-policy");

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-7",
            Name = SupportToolDefinitions.SearchKnowledgeBaseToolName,
            ArgumentsJson = "{\"query\":\"refund policy\"}"
        });

        Assert.True(result.Success);
        Assert.Contains("Refund Policy", result.Content, StringComparison.Ordinal);
        Assert.Contains("internal-policy", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyKnowledgeResult_WhenNothingMatches()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsEmptyKnowledgeResult_WhenNothingMatches));
        var knowledgeService = KnowledgeServiceTestFactory.Create(context);
        await knowledgeService.AddDocumentAsync(
            "Shipping Policy",
            "Standard orders usually ship within 1 to 2 business days.");

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-8",
            Name = SupportToolDefinitions.SearchKnowledgeBaseToolName,
            ArgumentsJson = "{\"query\":\"nonexistent topic\"}"
        });

        Assert.True(result.Success);
        Assert.Contains("\"results\":[]", result.Content.Replace(" ", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsStructuredError_WhenKnowledgeQueryIsEmpty()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(ExecuteAsync_ReturnsStructuredError_WhenKnowledgeQueryIsEmpty));

        var executor = ToolExecutorTestFactory.CreateExecutor(context);

        var result = await executor.ExecuteAsync(new AIToolCall
        {
            Id = "call-9",
            Name = SupportToolDefinitions.SearchKnowledgeBaseToolName,
            ArgumentsJson = "{\"query\":\"   \"}"
        });

        Assert.False(result.Success);
        Assert.Contains("query", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}

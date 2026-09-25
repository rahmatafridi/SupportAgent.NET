using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Tests;

public class AIGatewayToolLoopTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsDirectAnswer_WhenModelDoesNotRequestTools()
    {
        var provider = new ScriptableAIProvider(callNumber =>
            new AIResponse
            {
                Text = "RAG combines retrieval with generation.",
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10)
            });

        var gateway = CreateGateway(provider, new RecordingToolExecutor());

        var response = await gateway.GenerateAsync(new AIRequest
        {
            Prompt = "Explain RAG in one sentence.",
            Tools = SupportToolDefinitions.GetAll()
        });

        Assert.Equal("RAG combines retrieval with generation.", response.Text);
        Assert.Empty(response.ToolsUsed);
        Assert.Empty(response.Sources);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_ExecutesToolSelectedByProvider_AndReturnsFinalAnswer()
    {
        var provider = new ScriptableAIProvider(callNumber => callNumber switch
        {
            1 => new AIResponse
            {
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10),
                ToolCalls =
                [
                    new AIToolCall
                    {
                        Id = "call-1",
                        Name = SupportToolDefinitions.GetCustomerToolName,
                        ArgumentsJson = "{\"customerId\":101}"
                    }
                ]
            },
            _ => new AIResponse
            {
                Text = "Customer 101 is John Smith.",
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10)
            }
        });

        var toolExecutor = new RecordingToolExecutor();
        var gateway = CreateGateway(provider, toolExecutor);

        var response = await gateway.GenerateAsync(new AIRequest
        {
            Prompt = "Who is customer 101?",
            Tools = SupportToolDefinitions.GetAll()
        });

        Assert.Equal("Customer 101 is John Smith.", response.Text);
        Assert.Single(response.ToolsUsed);
        Assert.Equal(SupportToolDefinitions.GetCustomerToolName, response.ToolsUsed[0].Name);
        Assert.Equal(1, toolExecutor.ExecutionCount);
        Assert.Equal(2, provider.CallCount);
        Assert.Equal(SupportToolDefinitions.GetCustomerToolName, toolExecutor.LastToolCall?.Name);
    }

    [Fact]
    public async Task GenerateAsync_AppliesStructuredFormatOnlyAfterToolExecution()
    {
        var provider = new ScriptableAIProvider(callNumber => callNumber == 1
            ? new AIResponse
            {
                Provider = "Fake",
                Model = "fake-model",
                ToolCalls =
                [
                    new AIToolCall
                    {
                        Id = "call-order",
                        Name = SupportToolDefinitions.GetOrderStatusToolName,
                        ArgumentsJson = "{\"orderId\":2}"
                    }
                ]
            }
            : new AIResponse
            {
                Text = "{\"answer\":\"Order is shipped.\",\"suggestedActions\":[],\"confidence\":0.9}",
                Provider = "Fake",
                Model = "fake-model"
            });

        var schema = SupportAgentCopilotPrompts.SupportResponseJsonSchema;
        await CreateGateway(provider, new RecordingToolExecutor()).GenerateAsync(new AIRequest
        {
            Prompt = "What is happening?",
            Tools = SupportToolDefinitions.GetAll(),
            ResponseFormatJsonSchema = schema
        });

        Assert.Equal(3, provider.CallCount);
        Assert.All(provider.Requests.Take(2), nativeRequest =>
        {
            Assert.NotEmpty(nativeRequest.Tools!);
            Assert.Null(nativeRequest.ResponseFormatJsonSchema);
        });
        Assert.Null(provider.Requests[2].Tools);
        Assert.Equal(schema, provider.Requests[2].ResponseFormatJsonSchema);
        Assert.DoesNotContain(provider.Requests, item =>
            item.Tools is { Count: > 0 } && !string.IsNullOrWhiteSpace(item.ResponseFormatJsonSchema));
    }

    [Fact]
    public async Task GenerateAsync_ExecutesGetOrderStatus_WhenProviderRequestsIt()
    {
        var provider = new ScriptableAIProvider(callNumber => callNumber switch
        {
            1 => new AIResponse
            {
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10),
                ToolCalls =
                [
                    new AIToolCall
                    {
                        Id = "call-2",
                        Name = SupportToolDefinitions.GetOrderStatusToolName,
                        ArgumentsJson = "{\"orderId\":1}"
                    }
                ]
            },
            _ => new AIResponse
            {
                Text = "Order 1 is Processing.",
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10)
            }
        });

        var toolExecutor = new RecordingToolExecutor();
        var gateway = CreateGateway(provider, toolExecutor);

        var response = await gateway.GenerateAsync(new AIRequest
        {
            Prompt = "What is the status of order 1?",
            Tools = SupportToolDefinitions.GetAll()
        });

        Assert.Equal("Order 1 is Processing.", response.Text);
        Assert.Equal(SupportToolDefinitions.GetOrderStatusToolName, response.ToolsUsed[0].Name);
        Assert.Equal(SupportToolDefinitions.GetOrderStatusToolName, toolExecutor.LastToolCall?.Name);
    }

    [Fact]
    public async Task GenerateAsync_ExecutesSearchKnowledgeBase_WhenProviderRequestsIt()
    {
        var provider = new ScriptableAIProvider(callNumber => callNumber switch
        {
            1 => new AIResponse
            {
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10),
                ToolCalls =
                [
                    new AIToolCall
                    {
                        Id = "call-kb",
                        Name = SupportToolDefinitions.SearchKnowledgeBaseToolName,
                        ArgumentsJson = "{\"query\":\"refund policy\"}"
                    }
                ]
            },
            _ => new AIResponse
            {
                Text = "Customers may request a refund within 30 days.",
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10)
            }
        });

        var toolExecutor = new RecordingToolExecutor();
        var gateway = CreateGateway(provider, toolExecutor);

        var response = await gateway.GenerateAsync(new AIRequest
        {
            Prompt = "What is our refund policy?",
            Tools = SupportToolDefinitions.GetAll()
        });

        Assert.Equal("Customers may request a refund within 30 days.", response.Text);
        Assert.Equal(SupportToolDefinitions.SearchKnowledgeBaseToolName, response.ToolsUsed[0].Name);
        Assert.Single(response.Sources);
        Assert.Equal("Refund Policy", response.Sources[0].Title);
        Assert.Equal("internal-policy", response.Sources[0].Source);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_ExecutesMultipleTools_WhenProviderRequestsThem()
    {
        var provider = new ScriptableAIProvider(callNumber => callNumber switch
        {
            1 => new AIResponse
            {
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10),
                ToolCalls =
                [
                    new AIToolCall
                    {
                        Id = "call-customer",
                        Name = SupportToolDefinitions.GetCustomerToolName,
                        ArgumentsJson = "{\"customerId\":101}"
                    },
                    new AIToolCall
                    {
                        Id = "call-order",
                        Name = SupportToolDefinitions.GetOrderStatusToolName,
                        ArgumentsJson = "{\"orderId\":1}"
                    },
                    new AIToolCall
                    {
                        Id = "call-knowledge",
                        Name = SupportToolDefinitions.SearchKnowledgeBaseToolName,
                        ArgumentsJson = "{\"query\":\"shipping delayed order policy\"}"
                    }
                ]
            },
            _ => new AIResponse
            {
                Text = "Customer 101's order 1 is still processing. Shipping usually takes 1 to 2 business days.",
                Provider = "Fake",
                Model = "fake-model",
                Duration = TimeSpan.FromMilliseconds(10)
            }
        });

        var toolExecutor = new RecordingToolExecutor();
        var gateway = CreateGateway(provider, toolExecutor);

        var response = await gateway.GenerateAsync(new AIRequest
        {
            Prompt = "Customer 101 says order 1 hasn't arrived. What should I tell them?",
            Tools = SupportToolDefinitions.GetAll()
        });

        Assert.Equal(3, response.ToolsUsed.Count);
        Assert.Equal(3, toolExecutor.ExecutionCount);
        Assert.Single(response.Sources);
        Assert.Contains("Shipping Policy", response.Sources[0].Title, StringComparison.Ordinal);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenMaximumToolIterationsExceeded()
    {
        var provider = new ScriptableAIProvider(_ => new AIResponse
        {
            Provider = "Fake",
            Model = "fake-model",
            Duration = TimeSpan.FromMilliseconds(10),
            ToolCalls =
            [
                new AIToolCall
                {
                    Id = "call-loop",
                    Name = SupportToolDefinitions.GetCustomerToolName,
                    ArgumentsJson = "{\"customerId\":101}"
                }
            ]
        });

        var gateway = CreateGateway(provider, new RecordingToolExecutor());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.GenerateAsync(new AIRequest
            {
                Prompt = "Who is customer 101?",
                Tools = SupportToolDefinitions.GetAll()
            }));

        Assert.Contains("Maximum tool iterations", exception.Message, StringComparison.Ordinal);
        Assert.Equal(SupportToolDefinitions.MaximumToolIterations, provider.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_RespectsCancellationToken()
    {
        var provider = new ScriptableAIProvider(_ => new AIResponse
        {
            Provider = "Fake",
            Model = "fake-model",
            Duration = TimeSpan.FromMilliseconds(10),
            ToolCalls =
            [
                new AIToolCall
                {
                    Id = "call-cancel",
                    Name = SupportToolDefinitions.GetCustomerToolName,
                    ArgumentsJson = "{\"customerId\":101}"
                }
            ]
        });

        var gateway = CreateGateway(provider, new RecordingToolExecutor());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gateway.GenerateAsync(
                new AIRequest
                {
                    Prompt = "Who is customer 101?",
                    Tools = SupportToolDefinitions.GetAll()
                },
                cancellationTokenSource.Token));
    }

    private static AIGatewayService CreateGateway(
        ScriptableAIProvider provider,
        IAIToolExecutor toolExecutor)
    {
        var options = Options.Create(new AIOptions
        {
            Provider = "Fake"
        });

        return new AIGatewayService(
            [provider],
            options,
            toolExecutor,
            NullLogger<AIGatewayService>.Instance);
    }

    private sealed class ScriptableAIProvider(Func<int, AIResponse> responseFactory) : IAIProvider
    {
        public int CallCount { get; private set; }

        public List<AIRequest> Requests { get; } = [];

        public string ProviderName => "Fake";

        public Task<AIResponse> GenerateAsync(
            AIRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(CallCount));
        }
    }

    private sealed class RecordingToolExecutor : IAIToolExecutor
    {
        public int ExecutionCount { get; private set; }

        public AIToolCall? LastToolCall { get; private set; }

        public Task<AIToolResult> ExecuteAsync(
            AIToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            LastToolCall = toolCall;

            var content = toolCall.Name switch
            {
                SupportToolDefinitions.SearchKnowledgeBaseToolName when toolCall.ArgumentsJson.Contains(
                    "shipping",
                    StringComparison.OrdinalIgnoreCase) => """
                    {
                      "success": true,
                      "results": [
                        {
                          "documentTitle": "Shipping Policy",
                          "documentSource": "internal-policy",
                          "content": "Standard orders usually ship within 1 to 2 business days.",
                          "score": 0.88
                        }
                      ]
                    }
                    """,
                SupportToolDefinitions.SearchKnowledgeBaseToolName => """
                    {
                      "success": true,
                      "results": [
                        {
                          "documentTitle": "Refund Policy",
                          "documentSource": "internal-policy",
                          "content": "Customers may request a refund within 30 days.",
                          "score": 0.91
                        }
                      ]
                    }
                    """,
                _ => "{\"success\":true}"
            };

            return Task.FromResult(new AIToolResult
            {
                ToolCallId = toolCall.Id,
                ToolName = toolCall.Name,
                Success = true,
                Content = content
            });
        }
    }
}

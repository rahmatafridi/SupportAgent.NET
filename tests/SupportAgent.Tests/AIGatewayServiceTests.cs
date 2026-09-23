using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.AI.Tools;

namespace SupportAgent.Tests;

public class AIGatewayServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesConfiguredProvider_WhenToolsAreNotRequested()
    {
        var fakeProvider = new FakeAIProvider("Ollama", "Configured provider response.");
        var gateway = CreateGateway("Ollama", fakeProvider);

        var response = await gateway.GenerateAsync(new AIRequest
        {
            Prompt = "Hello"
        });

        Assert.Equal("Configured provider response.", response.Text);
        Assert.Equal("Ollama", response.Provider);
        Assert.Equal("fake-model", response.Model);
        Assert.Equal(1, fakeProvider.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenProviderIsNotRegistered()
    {
        var fakeProvider = new FakeAIProvider("Ollama", "response");
        var gateway = CreateGateway("OpenAI", fakeProvider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.GenerateAsync(new AIRequest { Prompt = "Hello" }));

        Assert.Contains("OpenAI", exception.Message, StringComparison.Ordinal);
    }

    private static AIGatewayService CreateGateway(string providerName, params IAIProvider[] providers)
    {
        var options = Options.Create(new AIOptions
        {
            Provider = providerName
        });

        return new AIGatewayService(
            providers,
            options,
            new NoOpToolExecutor(),
            NullLogger<AIGatewayService>.Instance);
    }

    private sealed class FakeAIProvider(string providerName, string responseText) : IAIProvider
    {
        public int CallCount { get; private set; }

        public string ProviderName => providerName;

        public Task<AIResponse> GenerateAsync(
            AIRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(new AIResponse
            {
                Text = responseText,
                Provider = providerName,
                Model = "fake-model",
                InputTokens = 5,
                OutputTokens = 10,
                TotalTokens = 15,
                Duration = TimeSpan.FromMilliseconds(100)
            });
        }
    }

    private sealed class NoOpToolExecutor : IAIToolExecutor
    {
        public Task<AIToolResult> ExecuteAsync(
            AIToolCall toolCall,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Tool execution was not expected in this test.");
    }
}

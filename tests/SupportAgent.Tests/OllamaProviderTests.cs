using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.AI.Providers;
using SupportAgent.Infrastructure.AI.Tools;

namespace SupportAgent.Tests;

public class OllamaProviderTests
{
    [Fact]
    public async Task GenerateAsync_ParsesNativeToolCalls_FromMessageToolCallsField()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/show")
            {
                return JsonResponse(new { capabilities = new[] { "tools" } });
            }

            return JsonResponse(new
            {
                model = "llama3.1",
                message = new
                {
                    role = "assistant",
                    content = "",
                    tool_calls = new[]
                    {
                        new
                        {
                            id = "call-1",
                            type = "function",
                            function = new
                            {
                                name = "GetCustomer",
                                arguments = new { customerId = 101 }
                            }
                        }
                    }
                }
            });
        });

        var provider = CreateProvider(handler);

        var response = await provider.GenerateAsync(new AIRequest
        {
            Prompt = "Who is customer 101?",
            Tools = SupportToolDefinitions.GetAll()
        });

        Assert.Single(response.ToolCalls);
        Assert.Equal("GetCustomer", response.ToolCalls[0].Name);
        Assert.Contains("101", response.ToolCalls[0].ArgumentsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_Throws_WhenModelReturnsJsonToolTextInsteadOfNativeToolCalls()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/show")
            {
                return JsonResponse(new { capabilities = new[] { "tools" } });
            }

            return JsonResponse(new
            {
                model = "qwen2.5-coder:7b",
                message = new
                {
                    role = "assistant",
                    content = "{\"name\": \"GetCustomer\", \"arguments\": {\"customerId\": 101}}"
                }
            });
        });

        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GenerateAsync(new AIRequest
            {
                Prompt = "Who is customer 101?",
                Tools = SupportToolDefinitions.GetAll()
            }));

        Assert.Contains("tool_calls", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qwen2.5-coder:7b", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsNaturalLanguage_WhenNoToolsAreRequested()
    {
        var handler = new StubHttpMessageHandler(_ =>
            JsonResponse(new
            {
                model = "qwen2.5-coder:7b",
                message = new
                {
                    role = "assistant",
                    content = "Customer 101 is John Smith."
                }
            }));

        var provider = CreateProvider(handler);

        var response = await provider.GenerateAsync(new AIRequest
        {
            Prompt = "Who is customer 101?"
        });

        Assert.Equal("Customer 101 is John Smith.", response.Text);
        Assert.Empty(response.ToolCalls);
    }

    [Fact]
    public void LooksLikeNonNativeToolCallText_DetectsJsonToolRequest()
    {
        var content = "{\"name\": \"GetCustomer\", \"arguments\": {\"customerId\": 101}}";

        Assert.True(OllamaAssistantToolTextDetector.LooksLikeNonNativeToolCallText(content));
    }

    private static OllamaProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };

        return new OllamaProvider(
            httpClient,
            Options.Create(new AIOptions
            {
                Ollama = new OllamaOptions
                {
                    Model = "qwen2.5-coder:7b"
                }
            }),
            new TestHostEnvironment(),
            NullLogger<OllamaProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload)
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "SupportAgent.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}

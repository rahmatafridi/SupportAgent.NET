using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Enums;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.AI.Providers;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Tests;

public class OllamaProviderTests
{
    [Fact]
    public async Task GenerateAsync_UsesDeterministicSamplingForCopilotRequests()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(new
            {
                model = "llama3.1",
                message = new { role = "assistant", content = "Stable answer" }
            });
        });

        await CreateProvider(handler).GenerateAsync(new AIRequest { Prompt = "Same question" });

        using var document = JsonDocument.Parse(requestBody!);
        var options = document.RootElement.GetProperty("options");
        Assert.Equal(0, options.GetProperty("temperature").GetInt32());
        Assert.Equal(42, options.GetProperty("seed").GetInt32());
    }

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
    public async Task GenerateAsync_ReplaysAssistantToolArgumentsAsJsonObject()
    {
        string? chatRequestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/show")
            {
                return JsonResponse(new { capabilities = new[] { "tools" } });
            }

            chatRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(new
            {
                model = "llama3.1",
                message = new { role = "assistant", content = "Customer 101 is John Smith." }
            });
        });

        await CreateProvider(handler).GenerateAsync(new AIRequest
        {
            Messages =
            [
                new AIMessage
                {
                    Role = AIMessageRole.Assistant,
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
                new AIMessage
                {
                    Role = AIMessageRole.Tool,
                    ToolCallId = "call-1",
                    ToolName = SupportToolDefinitions.GetCustomerToolName,
                    Content = "{\"id\":101,\"name\":\"John Smith\"}"
                }
            ],
            Tools = SupportToolDefinitions.GetAll()
        });

        using var document = JsonDocument.Parse(chatRequestBody!);
        var arguments = document.RootElement
            .GetProperty("messages")[0]
            .GetProperty("tool_calls")[0]
            .GetProperty("function")
            .GetProperty("arguments");

        Assert.Equal(JsonValueKind.Object, arguments.ValueKind);
        Assert.Equal(101, arguments.GetProperty("customerId").GetInt32());
    }

    [Fact]
    public async Task GenerateAsync_SendsJsonSchemaFormatForConstrainedFinalResponse()
    {
        string? chatRequestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/show")
            {
                return JsonResponse(new { capabilities = new[] { "tools" } });
            }

            chatRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(new
            {
                model = "llama3.1",
                message = new
                {
                    role = "assistant",
                    content = "{\"answer\":\"Order is shipped.\",\"suggestedActions\":[],\"confidence\":0.9}"
                }
            });
        });

        await CreateProvider(handler).GenerateAsync(new AIRequest
        {
            Messages = [new AIMessage { Role = AIMessageRole.Tool, ToolName = "GetOrderStatus", Content = "{}" }],
            ResponseFormatJsonSchema = SupportAgentCopilotPrompts.SupportResponseJsonSchema
        });

        using var document = JsonDocument.Parse(chatRequestBody!);
        var format = document.RootElement.GetProperty("format");
        Assert.Equal("object", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("properties").TryGetProperty("answer", out _));
    }

    [Fact]
    public async Task GenerateAsync_RejectsCombiningNativeToolsAndStructuredFormat()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP must not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProvider(handler).GenerateAsync(new AIRequest
            {
                Prompt = "Test",
                Tools = SupportToolDefinitions.GetAll(),
                ResponseFormatJsonSchema = SupportAgentCopilotPrompts.SupportResponseJsonSchema
            }));

        Assert.Contains("cannot be sent in the same", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_AcceptsModernObjectToolCapability()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri?.AbsolutePath == "/api/show"
            ? JsonResponse(new { capabilities = new { tools = true, vision = false } })
            : JsonResponse(new { model = "llama3.1", message = new { role = "assistant", content = "No tool needed." } }));
        var response = await CreateProvider(handler).GenerateAsync(new AIRequest { Prompt = "Hello", Tools = SupportToolDefinitions.GetAll() });
        Assert.Equal("No tool needed.", response.Text);
    }

    [Fact]
    public async Task GenerateAsync_AcceptsLegacyTemplateOnlyWhenNativeToolsVariableIsExplicit()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri?.AbsolutePath == "/api/show"
            ? JsonResponse(new { template = "{{ if .Tools }}{{ .Tools }}{{ end }}" })
            : JsonResponse(new { model = "llama3.1", message = new { role = "assistant", content = "Supported." } }));
        var response = await CreateProvider(handler).GenerateAsync(new AIRequest { Prompt = "Hello", Tools = SupportToolDefinitions.GetAll() });
        Assert.Equal("Supported.", response.Text);
    }

    [Fact]
    public async Task GenerateAsync_RejectsExplicitCapabilityListWithoutTools()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(new { capabilities = new[] { "completion", "vision" } }));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider(handler).GenerateAsync(new AIRequest { Prompt = "Hello", Tools = SupportToolDefinitions.GetAll() }));
        Assert.Contains("does not support native tool calling", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_ReportsMissingModelInsteadOfCapabilityVerificationFailure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { error = "model 'llama3.1:latest' not found" }) });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateProvider(handler).GenerateAsync(new AIRequest { Prompt = "Hello", Tools = SupportToolDefinitions.GetAll() }));
        Assert.Contains("is not installed", exception.Message);
        Assert.Contains("ollama pull", exception.Message);
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

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;

namespace SupportAgent.Infrastructure.AI.Providers;

/// <summary>
/// OpenAI AI provider. Sends chat requests to the OpenAI Chat Completions API and supports native tool calling.
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIProvider> _logger;

    /// <summary>
    /// Creates a new OpenAI provider instance.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with the OpenAI base URL.</param>
    /// <param name="options">AI configuration options.</param>
    /// <param name="logger">Logger used for provider diagnostics.</param>
    public OpenAIProvider(
        HttpClient httpClient,
        IOptions<AIOptions> options,
        ILogger<OpenAIProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.OpenAI;
        _logger = logger;
    }

    /// <summary>Provider identifier returned in AI responses.</summary>
    public string ProviderName => "OpenAI";

    /// <inheritdoc />
    public async Task<AIResponse> GenerateAsync(
        AIRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Set AI:OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        var stopwatch = Stopwatch.StartNew();
        var messages = AIMessageBuilder.BuildMessages(request);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = BuildOpenAIMessages(messages)
        };

        if (request.Tools is { Count: > 0 })
        {
            payload["tools"] = request.Tools.Select(BuildOpenAITool).ToList();
            payload["tool_choice"] = "auto";
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var chatResponse = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(
            cancellationToken: cancellationToken);

        var choice = chatResponse?.Choices?.FirstOrDefault();
        if (choice?.Message is null)
        {
            _logger.LogError("OpenAI returned an empty response.");
            throw new InvalidOperationException("OpenAI returned an empty response.");
        }

        stopwatch.Stop();

        var inputTokens = chatResponse?.Usage?.PromptTokens ?? 0;
        var outputTokens = chatResponse?.Usage?.CompletionTokens ?? 0;
        var totalTokens = chatResponse?.Usage?.TotalTokens ?? inputTokens + outputTokens;
        var toolCalls = ParseToolCalls(choice.Message);

        if (toolCalls.Count > 0)
        {
            return new AIResponse
            {
                Text = choice.Message.Content,
                ToolCalls = toolCalls,
                Provider = ProviderName,
                Model = chatResponse?.Model ?? _options.Model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = totalTokens,
                Duration = stopwatch.Elapsed
            };
        }

        if (string.IsNullOrWhiteSpace(choice.Message.Content))
        {
            _logger.LogError("OpenAI returned an empty response.");
            throw new InvalidOperationException("OpenAI returned an empty response.");
        }

        return new AIResponse
        {
            Text = choice.Message.Content,
            Provider = ProviderName,
            Model = chatResponse?.Model ?? _options.Model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            Duration = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Converts provider-neutral chat messages into OpenAI Chat Completions message objects.
    /// </summary>
    private static List<object> BuildOpenAIMessages(IEnumerable<AIMessageBuilder.ProviderChatMessage> messages) =>
        messages.Select(message =>
        {
            if (message.Role == "tool")
            {
                return (object)new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = message.ToolCallId ?? string.Empty,
                    ["content"] = message.Content ?? string.Empty
                };
            }

            if (message.ToolCalls is { Count: > 0 })
            {
                return new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = message.Content,
                    ["tool_calls"] = message.ToolCalls.Select(toolCall => new Dictionary<string, object?>
                    {
                        ["id"] = toolCall.Id,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object?>
                        {
                            ["name"] = toolCall.Name,
                            ["arguments"] = toolCall.ArgumentsJson
                        }
                    }).ToList()
                };
            }

            return new Dictionary<string, object?>
            {
                ["role"] = message.Role,
                ["content"] = message.Content ?? string.Empty
            };
        }).ToList();

    /// <summary>
    /// Converts one support tool definition into OpenAI's tool schema format.
    /// </summary>
    private static object BuildOpenAITool(AIToolDefinition tool) =>
        new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = JsonSerializer.Deserialize<JsonElement>(tool.ParametersJsonSchema)
            }
        };

    /// <summary>
    /// Parses tool calls returned by the OpenAI Chat Completions API.
    /// </summary>
    private static IReadOnlyList<AIToolCall> ParseToolCalls(OpenAIMessage message)
    {
        if (message.ToolCalls is null || message.ToolCalls.Count == 0)
        {
            return [];
        }

        return message.ToolCalls
            .Where(toolCall => toolCall.Function is not null)
            .Select(toolCall => new AIToolCall
            {
                Id = toolCall.Id ?? Guid.NewGuid().ToString("N"),
                Name = toolCall.Function!.Name ?? string.Empty,
                ArgumentsJson = toolCall.Function.Arguments ?? "{}"
            })
            .ToList();
    }

    private sealed class OpenAIChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private sealed class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    private sealed class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAIToolCall>? ToolCalls { get; set; }
    }

    private sealed class OpenAIToolCall
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("function")]
        public OpenAIToolFunction? Function { get; set; }
    }

    private sealed class OpenAIToolFunction
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }

    private sealed class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}

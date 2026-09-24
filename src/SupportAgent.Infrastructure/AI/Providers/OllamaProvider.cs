using System.Diagnostics;

using System.Net.Http.Json;

using System.Text.Json;

using System.Text.Json.Serialization;

using Microsoft.Extensions.Hosting;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;

using SupportAgent.Core.Interfaces;

using SupportAgent.Core.Models;

using SupportAgent.Core.Models.AI;



namespace SupportAgent.Infrastructure.AI.Providers;



/// <summary>

/// Ollama AI provider. Sends chat requests to a local or remote Ollama server and supports native tool calling.

/// </summary>

public class OllamaProvider : IAIProvider

{

    private readonly HttpClient _httpClient;

    private readonly OllamaOptions _options;

    private readonly IHostEnvironment _hostEnvironment;

    private readonly ILogger<OllamaProvider> _logger;



    /// <summary>

    /// Creates a new Ollama provider instance.

    /// </summary>

    /// <param name="httpClient">HTTP client configured with the Ollama base URL.</param>

    /// <param name="options">AI configuration options.</param>

    /// <param name="hostEnvironment">Host environment used for development diagnostics.</param>

    /// <param name="logger">Logger used for provider diagnostics.</param>

    public OllamaProvider(

        HttpClient httpClient,

        IOptions<AIOptions> options,

        IHostEnvironment hostEnvironment,

        ILogger<OllamaProvider> logger)

    {

        _httpClient = httpClient;

        _options = options.Value.Ollama;

        _hostEnvironment = hostEnvironment;

        _logger = logger;

    }



    /// <summary>Provider identifier returned in AI responses.</summary>

    public string ProviderName => "Ollama";



    /// <inheritdoc />

    public async Task<AIResponse> GenerateAsync(
        AIRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Tools is { Count: > 0 })
        {
            await EnsureToolCallingSupportedAsync(cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var messages = AIMessageBuilder.BuildMessages(request);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = BuildOllamaMessages(messages),
            ["stream"] = false
        };



        if (request.Tools is { Count: > 0 })

        {

            payload["tools"] = request.Tools.Select(BuildOllamaTool).ToList();

        }



        using var response = await _httpClient.PostAsJsonAsync(

            "/api/chat",

            payload,

            cancellationToken);



        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);



        if (!response.IsSuccessStatusCode)

        {

            if (request.Tools is { Count: > 0 })

            {

                throw new InvalidOperationException(
                    $"Ollama model '{_options.Model}' does not support native tool calling or returned a tool error: {responseBody}");

            }



            response.EnsureSuccessStatusCode();

        }



        var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody);

        if (chatResponse?.Message is null)

        {

            _logger.LogError("Ollama returned an empty response.");

            throw new InvalidOperationException("Ollama returned an empty response.");

        }



        stopwatch.Stop();



        var inputTokens = chatResponse.PromptEvalCount ?? 0;

        var outputTokens = chatResponse.EvalCount ?? 0;

        var toolCalls = ParseToolCalls(chatResponse.Message);

        var modelName = chatResponse.Model ?? _options.Model;



        LogDevelopmentDiagnostics(modelName, toolCalls);



        if (request.Tools is { Count: > 0 } &&

            toolCalls.Count == 0 &&

            OllamaAssistantToolTextDetector.LooksLikeNonNativeToolCallText(chatResponse.Message.Content))

        {

            throw new InvalidOperationException(

                $"Ollama model '{modelName}' returned a tool request in assistant text instead of native tool_calls. " +

                "This model is not performing native Ollama tool calling. " +

                "Use a model that populates message.tool_calls, such as llama3.1.");

        }



        if (toolCalls.Count > 0)

        {

            return new AIResponse

            {

                Text = chatResponse.Message.Content,

                ToolCalls = toolCalls,

                Provider = ProviderName,

                Model = modelName,

                InputTokens = inputTokens,

                OutputTokens = outputTokens,

                TotalTokens = inputTokens + outputTokens,

                Duration = stopwatch.Elapsed

            };

        }



        if (string.IsNullOrWhiteSpace(chatResponse.Message.Content))

        {

            _logger.LogError("Ollama returned an empty response.");

            throw new InvalidOperationException("Ollama returned an empty response.");

        }



        return new AIResponse

        {

            Text = chatResponse.Message.Content,

            Provider = ProviderName,

            Model = modelName,

            InputTokens = inputTokens,

            OutputTokens = outputTokens,

            TotalTokens = inputTokens + outputTokens,

            Duration = stopwatch.Elapsed

        };

    }



    /// <summary>

    /// Logs native tool-call diagnostics in Development only.

    /// </summary>

    private void LogDevelopmentDiagnostics(string modelName, IReadOnlyList<AIToolCall> toolCalls)

    {

        if (!_hostEnvironment.IsDevelopment())

        {

            return;

        }



        _logger.LogInformation(

            "Ollama chat response: model={Model}, tool_calls_returned={ToolCallsReturned}, tool_call_count={ToolCallCount}",

            modelName,

            toolCalls.Count > 0,

            toolCalls.Count);

    }



    /// <summary>
    /// Verifies that the configured Ollama model supports native tool calling before sending tool requests.
    /// </summary>
    private async Task EnsureToolCallingSupportedAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "/api/show",
            new { model = _options.Model, verbose = true },
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var serverError = TryReadError(body);
            if ((int)response.StatusCode == 404 ||
                serverError.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Ollama model '{_options.Model}' is not installed. Run 'ollama pull {_options.Model}' and try again.");
            }

            throw new InvalidOperationException(
                $"Unable to verify Ollama model '{_options.Model}' for native tool calling support. " +
                $"Ollama returned HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var hasCapabilities = root.TryGetProperty("capabilities", out var capabilities);
        var rawCapabilities = hasCapabilities ? capabilities.GetRawText() : "<missing>";
        var rawDetails = root.TryGetProperty("details", out var details) ? details.GetRawText() : "<missing>";
        var templateHasNativeTools = root.TryGetProperty("template", out var template) &&
            template.ValueKind == JsonValueKind.String &&
            template.GetString()!.Contains(".Tools", StringComparison.Ordinal);

        if (_hostEnvironment.IsDevelopment())
        {
            _logger.LogInformation(
                "Ollama /api/show capability metadata: model={Model}, capabilities={Capabilities}, details={Details}, template_has_native_tools={TemplateHasNativeTools}",
                _options.Model, rawCapabilities, rawDetails, templateHasNativeTools);
        }

        if (hasCapabilities)
        {
            if (HasToolCapability(capabilities)) return;
            throw new InvalidOperationException(
                $"Ollama model '{_options.Model}' does not support native tool calling. Choose a tool-capable model such as llama3.1 or qwen2.5.");
        }

        // Older Ollama versions did not return capabilities. Only accept their
        // response when the model template explicitly exposes Ollama's .Tools variable.
        if (templateHasNativeTools) return;

        throw new InvalidOperationException(
            $"Unable to verify Ollama model '{_options.Model}' for native tool calling support because /api/show returned no capability metadata.");
    }

    private static bool HasToolCapability(JsonElement capabilities)
    {
        if (capabilities.ValueKind == JsonValueKind.Array)
            return capabilities.EnumerateArray().Any(capability => capability.ValueKind == JsonValueKind.String &&
                string.Equals(capability.GetString(), "tools", StringComparison.OrdinalIgnoreCase));
        if (capabilities.ValueKind == JsonValueKind.Object && capabilities.TryGetProperty("tools", out var tools))
            return tools.ValueKind == JsonValueKind.True ||
                (tools.ValueKind == JsonValueKind.String && bool.TryParse(tools.GetString(), out var enabled) && enabled);
        return capabilities.ValueKind == JsonValueKind.String &&
            string.Equals(capabilities.GetString(), "tools", StringComparison.OrdinalIgnoreCase);
    }

    private static string TryReadError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException) { return string.Empty; }
    }



    /// <summary>

    /// Converts provider-neutral chat messages into Ollama chat message objects.

    /// </summary>

    private static List<object> BuildOllamaMessages(IEnumerable<AIMessageBuilder.ProviderChatMessage> messages) =>

        messages.Select(message =>

        {

            if (message.Role == "tool")

            {

                return (object)new Dictionary<string, object?>

                {

                    ["role"] = "tool",

                    ["content"] = message.Content ?? string.Empty,

                    ["tool_name"] = message.ToolName ?? string.Empty

                };

            }



            if (message.ToolCalls is { Count: > 0 })

            {

                return new Dictionary<string, object?>

                {

                    ["role"] = "assistant",

                    ["content"] = message.Content ?? string.Empty,

                    ["tool_calls"] = message.ToolCalls.Select(toolCall => new Dictionary<string, object?>

                    {

                        ["id"] = toolCall.Id,

                        ["type"] = "function",

                        ["function"] = new Dictionary<string, object?>

                        {

                            ["name"] = toolCall.Name,

                            // Ollama expects native function arguments to remain a JSON
                            // object when an assistant tool call is replayed. Sending the
                            // serialized JSON as a string causes modern model templates
                            // (including llama3.1) to try to parse a double-encoded value.
                            ["arguments"] = DeserializeToolArguments(toolCall.ArgumentsJson)

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

    private static JsonElement DeserializeToolArguments(string argumentsJson)
    {
        try
        {
            var arguments = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            return arguments.ValueKind == JsonValueKind.Object
                ? arguments
                : JsonSerializer.Deserialize<JsonElement>("{}");
        }
        catch (JsonException)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }
    }



    /// <summary>

    /// Converts one support tool definition into Ollama's tool schema format.

    /// </summary>

    private static object BuildOllamaTool(AIToolDefinition tool) =>

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

    /// Parses native tool calls returned only from Ollama's message.tool_calls field.

    /// </summary>

    private static IReadOnlyList<AIToolCall> ParseToolCalls(OllamaChatMessage message)

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

                ArgumentsJson = toolCall.Function!.Arguments is { } arguments
                    ? NormalizeArgumentsJson(arguments)
                    : "{}"

            })

            .ToList();

    }



    /// <summary>

    /// Normalizes Ollama function arguments whether returned as JSON text or an object.

    /// </summary>

    private static string NormalizeArgumentsJson(JsonElement arguments)
    {
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        return arguments.GetRawText();
    }



    private sealed class OllamaChatResponse

    {

        [JsonPropertyName("model")]

        public string? Model { get; set; }



        [JsonPropertyName("message")]

        public OllamaChatMessage? Message { get; set; }



        [JsonPropertyName("prompt_eval_count")]

        public int? PromptEvalCount { get; set; }



        [JsonPropertyName("eval_count")]

        public int? EvalCount { get; set; }

    }



    private sealed class OllamaChatMessage

    {

        [JsonPropertyName("role")]

        public string? Role { get; set; }



        [JsonPropertyName("content")]

        public string? Content { get; set; }



        [JsonPropertyName("tool_calls")]

        public List<OllamaToolCall>? ToolCalls { get; set; }

    }



    private sealed class OllamaToolCall

    {

        [JsonPropertyName("id")]

        public string? Id { get; set; }



        [JsonPropertyName("function")]

        public OllamaToolFunction? Function { get; set; }

    }



    private sealed class OllamaToolFunction

    {

        [JsonPropertyName("name")]

        public string? Name { get; set; }



        [JsonPropertyName("arguments")]
        public JsonElement Arguments { get; set; }

    }

}



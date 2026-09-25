using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Enums;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI.Tools;

namespace SupportAgent.Infrastructure.AI;

/// <summary>
/// Provider-independent AI gateway. Selects the configured provider and orchestrates native tool calling.
/// </summary>
public class AIGatewayService : IAIGateway
{
    private readonly IReadOnlyDictionary<string, IAIProvider> _providers;
    private readonly AIOptions _options;
    private readonly IAIToolExecutor _toolExecutor;
    private readonly ILogger<AIGatewayService> _logger;

    /// <summary>
    /// Creates a new AI gateway instance.
    /// </summary>
    /// <param name="providers">All registered AI providers.</param>
    /// <param name="options">AI configuration including the selected provider name.</param>
    /// <param name="toolExecutor">Executes tool calls selected by the model.</param>
    /// <param name="logger">Logger used for tool loop diagnostics.</param>
    public AIGatewayService(
        IEnumerable<IAIProvider> providers,
        IOptions<AIOptions> options,
        IAIToolExecutor toolExecutor,
        ILogger<AIGatewayService> logger)
    {
        _providers = providers.ToDictionary(
            provider => provider.ProviderName,
            StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _toolExecutor = toolExecutor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AIResponse> GenerateAsync(
        AIRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(_options.Provider, out var provider))
        {
            throw new InvalidOperationException(
                $"AI provider '{_options.Provider}' is not registered.");
        }

        if (request.Tools is null or { Count: 0 })
        {
            return await provider.GenerateAsync(request, cancellationToken);
        }

        return await ExecuteToolLoopAsync(provider, request, cancellationToken);
    }

    /// <summary>
    /// Runs the model/tool loop until the LLM returns a final answer or the maximum iteration limit is reached.
    /// </summary>
    /// <param name="provider">The configured AI provider.</param>
    /// <param name="request">The original AI request including tools.</param>
    /// <param name="cancellationToken">Token used to cancel provider and tool calls.</param>
    /// <returns>The final AI response including tools that were executed.</returns>
    private async Task<AIResponse> ExecuteToolLoopAsync(
        IAIProvider provider,
        AIRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = BuildConversation(request);
        var toolsUsed = new List<AIToolCall>();
        var toolExecutions = new List<AIToolExecutionRecord>();
        var sources = new List<KnowledgeSource>();
        var totalDuration = TimeSpan.Zero;
        var inputTokens = 0;
        var outputTokens = 0;
        string? model = null;
        string? providerName = null;

        for (var iteration = 0; iteration < SupportToolDefinitions.MaximumToolIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var iterationRequest = new AIRequest
            {
                SystemPrompt = request.SystemPrompt,
                Messages = conversation,
                Tools = request.Tools,
                // Native tool selection and final response formatting are separate
                // phases. Some local models treat a response schema as the argument
                // schema of the next function when both are sent together.
                ResponseFormatJsonSchema = null
            };

            var response = await provider.GenerateAsync(iterationRequest, cancellationToken);
            totalDuration += response.Duration;
            inputTokens += response.InputTokens;
            outputTokens += response.OutputTokens;
            model = response.Model;
            providerName = response.Provider;

            if (!response.HasToolCalls)
            {
                if (!string.IsNullOrWhiteSpace(request.ResponseFormatJsonSchema))
                {
                    conversation.Add(new AIMessage
                    {
                        Role = AIMessageRole.Assistant,
                        Content = response.Text ?? string.Empty
                    });
                    conversation.Add(new AIMessage
                    {
                        Role = AIMessageRole.User,
                        Content = "Return the final answer using the required structured response format. Do not call tools."
                    });

                    var formattedResponse = await provider.GenerateAsync(new AIRequest
                    {
                        SystemPrompt = request.SystemPrompt,
                        Messages = conversation,
                        Tools = null,
                        ResponseFormatJsonSchema = request.ResponseFormatJsonSchema
                    }, cancellationToken);

                    if (formattedResponse.HasToolCalls)
                    {
                        throw new InvalidOperationException(
                            "The provider returned a native tool call during the structured response formatting phase.");
                    }

                    totalDuration += formattedResponse.Duration;
                    inputTokens += formattedResponse.InputTokens;
                    outputTokens += formattedResponse.OutputTokens;
                    model = formattedResponse.Model;
                    providerName = formattedResponse.Provider;
                    response = formattedResponse;
                }

                return new AIResponse
                {
                    Text = response.Text,
                    Provider = providerName ?? string.Empty,
                    Model = model ?? string.Empty,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    TotalTokens = inputTokens + outputTokens,
                    Duration = totalDuration,
                    ToolsUsed = toolsUsed,
                    ToolExecutions = toolExecutions,
                    Sources = sources
                };
            }

            conversation.Add(new AIMessage
            {
                Role = AIMessageRole.Assistant,
                Content = response.Text ?? string.Empty,
                ToolCalls = response.ToolCalls
            });

            foreach (var toolCall in response.ToolCalls)
            {
                toolsUsed.Add(toolCall);
                var toolResult = await _toolExecutor.ExecuteAsync(toolCall, cancellationToken);
                toolExecutions.Add(new AIToolExecutionRecord
                {
                    Name = toolCall.Name,
                    ArgumentsJson = toolCall.ArgumentsJson,
                    Success = toolResult.Success,
                    Timestamp = DateTime.UtcNow
                });
                KnowledgeSourceParser.TryAddSourcesFromToolResult(
                    toolCall.Name,
                    toolResult.Content,
                    sources);

                conversation.Add(new AIMessage
                {
                    Role = AIMessageRole.Tool,
                    ToolCallId = toolResult.ToolCallId,
                    ToolName = toolResult.ToolName,
                    Content = toolResult.Content
                });
            }
        }

        _logger.LogWarning("Maximum tool iterations ({Maximum}) exceeded.", SupportToolDefinitions.MaximumToolIterations);
        throw new InvalidOperationException(
            $"Maximum tool iterations ({SupportToolDefinitions.MaximumToolIterations}) exceeded.");
    }

    /// <summary>
    /// Builds the initial conversation history from the incoming request.
    /// </summary>
    /// <param name="request">The incoming AI request.</param>
    /// <returns>A mutable conversation message list used by the tool loop.</returns>
    private static List<AIMessage> BuildConversation(AIRequest request)
    {
        var conversation = new List<AIMessage>();

        if (request.Messages is not null)
        {
            conversation.AddRange(request.Messages);
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            conversation.Add(new AIMessage
            {
                Role = AIMessageRole.User,
                Content = request.Prompt
            });
        }

        return conversation;
    }
}

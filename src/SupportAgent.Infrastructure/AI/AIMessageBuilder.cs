using SupportAgent.Core.Enums;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.AI;

/// <summary>
/// Builds provider-neutral chat messages from generic AI request and conversation objects.
/// </summary>
internal static class AIMessageBuilder
{
    /// <summary>
    /// Converts an <see cref="AIRequest"/> into a list of provider-ready chat messages.
    /// </summary>
    /// <param name="request">The AI request containing system prompt, conversation, and user prompt.</param>
    /// <returns>Messages in the order they should be sent to the provider.</returns>
    public static List<ProviderChatMessage> BuildMessages(AIRequest request)
    {
        var messages = new List<ProviderChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new ProviderChatMessage
            {
                Role = "system",
                Content = request.SystemPrompt
            });
        }

        if (request.Messages is not null)
        {
            foreach (var message in request.Messages)
            {
                messages.Add(MapMessage(message));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new ProviderChatMessage
            {
                Role = "user",
                Content = request.Prompt
            });
        }

        if (messages.Count == 0)
        {
            throw new InvalidOperationException("AI request must include a prompt or conversation messages.");
        }

        return messages;
    }

    /// <summary>
    /// Maps one generic conversation message to a provider-neutral chat message.
    /// </summary>
    /// <param name="message">The conversation message to map.</param>
    /// <returns>A provider-ready chat message.</returns>
    private static ProviderChatMessage MapMessage(AIMessage message) =>
        message.Role switch
        {
            AIMessageRole.System => new ProviderChatMessage
            {
                Role = "system",
                Content = message.Content
            },
            AIMessageRole.User => new ProviderChatMessage
            {
                Role = "user",
                Content = message.Content
            },
            AIMessageRole.Assistant => new ProviderChatMessage
            {
                Role = "assistant",
                Content = message.Content,
                ToolCalls = message.ToolCalls?
                    .Select(toolCall => new ProviderToolCall(
                        toolCall.Id,
                        toolCall.Name,
                        toolCall.ArgumentsJson))
                    .ToList()
            },
            AIMessageRole.Tool => new ProviderChatMessage
            {
                Role = "tool",
                Content = message.Content,
                ToolCallId = message.ToolCallId,
                ToolName = message.ToolName
            },
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Role, "Unsupported AI message role.")
        };

    /// <summary>
    /// Provider-neutral chat message used by Ollama and OpenAI providers.
    /// </summary>
    internal sealed class ProviderChatMessage
    {
        public required string Role { get; init; }

        public string? Content { get; init; }

        public string? ToolCallId { get; init; }

        public string? ToolName { get; init; }

        public IReadOnlyList<ProviderToolCall>? ToolCalls { get; init; }
    }

    /// <summary>
    /// Provider-neutral tool call attached to an assistant message.
    /// </summary>
    internal sealed record ProviderToolCall(
        string Id,
        string Name,
        string ArgumentsJson);
}

using SupportAgent.Core.Models.AI;

namespace SupportAgent.Core.Models;

/// <summary>
/// Response returned by the AI gateway after one provider call or a full tool-calling loop.
/// </summary>
public class AIResponse
{
    /// <summary>The assistant's text answer, if the model produced one.</summary>
    public string? Text { get; set; }

    /// <summary>Tool calls requested by the model in the latest provider response.</summary>
    public IReadOnlyList<AIToolCall> ToolCalls { get; set; } = [];

    /// <summary>All tools that were executed while answering the request.</summary>
    public IReadOnlyList<AIToolCall> ToolsUsed { get; set; } = [];

    /// <summary>Audit records for each tool execution in this request.</summary>
    public IReadOnlyList<AIToolExecutionRecord> ToolExecutions { get; set; } = [];

    /// <summary>Grounded knowledge sources cited from SearchKnowledgeBase tool results.</summary>
    public IReadOnlyList<KnowledgeSource> Sources { get; set; } = [];

    /// <summary>The provider that handled the request, such as Ollama or OpenAI.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The model name used for the request.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Total input tokens consumed across all provider calls.</summary>
    public int InputTokens { get; set; }

    /// <summary>Total output tokens consumed across all provider calls.</summary>
    public int OutputTokens { get; set; }

    /// <summary>Total tokens consumed across all provider calls.</summary>
    public int TotalTokens { get; set; }

    /// <summary>Total time spent generating the response.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>True when the latest provider response contains one or more tool calls.</summary>
    public bool HasToolCalls => ToolCalls.Count > 0;
}

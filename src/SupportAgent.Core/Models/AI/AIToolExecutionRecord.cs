namespace SupportAgent.Core.Models.AI;

/// <summary>
/// Audit information for one tool execution during an AI request.
/// </summary>
public class AIToolExecutionRecord
{
    /// <summary>Tool name that was executed.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON arguments passed to the tool.</summary>
    public string ArgumentsJson { get; set; } = "{}";

    /// <summary>Whether the tool execution succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>UTC timestamp when the tool was executed.</summary>
    public DateTime Timestamp { get; set; }
}

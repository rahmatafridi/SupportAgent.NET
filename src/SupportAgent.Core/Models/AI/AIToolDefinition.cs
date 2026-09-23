namespace SupportAgent.Core.Models.AI;

/// <summary>
/// Describes one callable tool exposed to the LLM during a chat request.
/// </summary>
public class AIToolDefinition
{
    /// <summary>The tool name the model must use when calling it, such as GetCustomer.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable description that helps the model decide when to use the tool.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>JSON Schema describing the tool's input parameters.</summary>
    public string ParametersJsonSchema { get; set; } = "{}";
}

namespace SupportAgent.Core.Models.AI;

/// <summary>
/// Describes one parameter accepted by a support tool.
/// </summary>
public class AIToolParameter
{
    /// <summary>The parameter name, such as customerId.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The parameter type, such as integer or string.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Description that helps the model supply the correct value.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>True when the parameter must be provided.</summary>
    public bool Required { get; set; }
}

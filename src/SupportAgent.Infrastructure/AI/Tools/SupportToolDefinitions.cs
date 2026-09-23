using SupportAgent.Core.Models.AI;

namespace SupportAgent.Infrastructure.AI.Tools;

/// <summary>
/// Built-in support tools available to the LLM.
/// </summary>
public static class SupportToolDefinitions
{
    /// <summary>
    /// Maximum number of model/tool iterations allowed before the gateway stops safely.
    /// </summary>
    public const int MaximumToolIterations = 5;

    /// <summary>
    /// Tool name used by the LLM to look up a customer by ID.
    /// </summary>
    public const string GetCustomerToolName = "GetCustomer";

    /// <summary>
    /// Tool name used by the LLM to look up an order by ID.
    /// </summary>
    public const string GetOrderStatusToolName = "GetOrderStatus";

    /// <summary>
    /// Tool name used by the LLM to search the company knowledge base.
    /// </summary>
    public const string SearchKnowledgeBaseToolName = "SearchKnowledgeBase";

    /// <summary>
    /// Returns all tools currently available to the AI assistant.
    /// </summary>
    /// <returns>The list of tool definitions sent to the model.</returns>
    public static IReadOnlyList<AIToolDefinition> GetAll() =>
    [
        GetCustomer,
        GetOrderStatus,
        SearchKnowledgeBase
    ];

    /// <summary>
    /// Tool definition for looking up a customer by ID.
    /// </summary>
    public static AIToolDefinition GetCustomer { get; } = new()
    {
        Name = GetCustomerToolName,
        Description = "Gets a customer using the customer ID.",
        ParametersJsonSchema = """
            {
              "type": "object",
              "properties": {
                "customerId": {
                  "type": "integer",
                  "description": "The customer ID"
                }
              },
              "required": ["customerId"],
              "additionalProperties": false
            }
            """
    };

    /// <summary>
    /// Tool definition for looking up an order by ID.
    /// </summary>
    public static AIToolDefinition GetOrderStatus { get; } = new()
    {
        Name = GetOrderStatusToolName,
        Description = "Gets the current order information and status using the order ID.",
        ParametersJsonSchema = """
            {
              "type": "object",
              "properties": {
                "orderId": {
                  "type": "integer",
                  "description": "The order ID"
                }
              },
              "required": ["orderId"],
              "additionalProperties": false
            }
            """
    };

    /// <summary>
    /// Tool definition for searching the internal company knowledge base.
    /// </summary>
    public static AIToolDefinition SearchKnowledgeBase { get; } = new()
    {
        Name = SearchKnowledgeBaseToolName,
        Description = """
            Searches the company's internal knowledge base for policies, procedures, product information,
            troubleshooting instructions, and other internal support knowledge.
            """,
        ParametersJsonSchema = """
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "The information to search for in the company knowledge base."
                }
              },
              "required": ["query"],
              "additionalProperties": false
            }
            """
    };
}

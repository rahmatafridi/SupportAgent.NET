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
        GetOrderStatus,
        GetCustomer,
        SearchKnowledgeBase
    ];

    /// <summary>
    /// Tool definition for looking up a customer by ID.
    /// </summary>
    public static AIToolDefinition GetCustomer { get; } = new()
    {
        Name = GetCustomerToolName,
        Description = "Retrieves customer identity or contact details only. Do not use it for a general ticket summary or order investigation merely because the question says customer; it cannot provide order status.",
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
        Description = "Gets authoritative current order information and status using the order ID. When ticket context provides a numeric RelatedOrderId and the linked order is relevant to the support question, use that exact ID before answering.",
        ParametersJsonSchema = """
            {
              "type": "object",
              "properties": {
                "orderId": {
                  "type": "integer",
                  "description": "The exact related order ID, such as RelatedOrderId from ticket context"
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
            Retrieves company policy, procedure, product information, or troubleshooting guidance only.
            It does not retrieve current customer or order facts and is not the first step for a linked entity status.
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

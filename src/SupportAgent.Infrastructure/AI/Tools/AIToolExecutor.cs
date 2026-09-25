using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models.AI;

namespace SupportAgent.Infrastructure.AI.Tools;

/// <summary>
/// Executes tool calls selected by the LLM. Maps tool names to normal business services.
/// </summary>
public class AIToolExecutor : IAIToolExecutor
{
    private static readonly HashSet<string> KnownTools =
    [
        SupportToolDefinitions.GetCustomerToolName,
        SupportToolDefinitions.GetOrderStatusToolName,
        SupportToolDefinitions.SearchKnowledgeBaseToolName
    ];

    private readonly ICustomerService _customerService;
    private readonly IOrderService _orderService;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<AIToolExecutor>? _logger;
    private readonly IHostEnvironment? _hostEnvironment;

    /// <summary>
    /// Creates a new tool executor instance.
    /// </summary>
    /// <param name="customerService">Business service used by the GetCustomer tool.</param>
    /// <param name="orderService">Business service used by the GetOrderStatus tool.</param>
    /// <param name="knowledgeService">Business service used by the SearchKnowledgeBase tool.</param>
    /// <param name="logger">Logger used for rejected native tool-call diagnostics.</param>
    /// <param name="hostEnvironment">Host environment controlling development-only diagnostics.</param>
    public AIToolExecutor(
        ICustomerService customerService,
        IOrderService orderService,
        IKnowledgeService knowledgeService,
        ILogger<AIToolExecutor>? logger = null,
        IHostEnvironment? hostEnvironment = null)
    {
        _customerService = customerService;
        _orderService = orderService;
        _knowledgeService = knowledgeService;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public async Task<AIToolResult> ExecuteAsync(
        AIToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toolCall.Name))
        {
            return CreateErrorResult(toolCall, "Tool name is required.");
        }

        if (!KnownTools.Contains(toolCall.Name))
        {
            return CreateErrorResult(
                toolCall,
                $"Tool '{toolCall.Name}' is not registered.");
        }

        var result = toolCall.Name switch
        {
            SupportToolDefinitions.GetCustomerToolName =>
                await ExecuteGetCustomerAsync(toolCall, cancellationToken),
            SupportToolDefinitions.GetOrderStatusToolName =>
                await ExecuteGetOrderStatusAsync(toolCall, cancellationToken),
            SupportToolDefinitions.SearchKnowledgeBaseToolName =>
                await ExecuteSearchKnowledgeBaseAsync(toolCall, cancellationToken),
            _ => CreateErrorResult(toolCall, $"Tool '{toolCall.Name}' is not registered.")
        };

        if (!result.Success && _hostEnvironment?.IsDevelopment() == true)
        {
            _logger?.LogWarning(
                "Rejected native tool call {ToolName}. Arguments: {Arguments}. Error: {Error}",
                toolCall.Name,
                toolCall.ArgumentsJson,
                result.Error);
        }

        return result;
    }

    /// <summary>
    /// Executes the GetCustomer tool by calling <see cref="ICustomerService.GetCustomerAsync"/>.
    /// </summary>
    private async Task<AIToolResult> ExecuteGetCustomerAsync(
        AIToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryReadIntArgument(toolCall.ArgumentsJson, "customerId", out var customerId))
        {
            return CreateErrorResult(toolCall, "Argument 'customerId' must be a valid integer.");
        }

        if (customerId <= 0)
        {
            return CreateErrorResult(toolCall, "Argument 'customerId' must be greater than zero.");
        }

        var customer = await _customerService.GetCustomerAsync(customerId, cancellationToken);
        if (customer is null)
        {
            return CreateErrorResult(toolCall, $"Customer {customerId} was not found.");
        }

        return CreateSuccessResult(toolCall, new
        {
            success = true,
            customer = new
            {
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                customer.Phone,
                customer.CreatedAt
            }
        });
    }

    /// <summary>
    /// Executes the GetOrderStatus tool by calling <see cref="IOrderService.GetOrderAsync"/>.
    /// </summary>
    private async Task<AIToolResult> ExecuteGetOrderStatusAsync(
        AIToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryReadIntArgument(toolCall.ArgumentsJson, "orderId", out var orderId))
        {
            return CreateErrorResult(toolCall, "Argument 'orderId' must be a valid integer.");
        }

        if (orderId <= 0)
        {
            return CreateErrorResult(toolCall, "Argument 'orderId' must be greater than zero.");
        }

        var order = await _orderService.GetOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return CreateErrorResult(toolCall, $"Order {orderId} was not found.");
        }

        return CreateSuccessResult(toolCall, new
        {
            success = true,
            order = new
            {
                order.Id,
                order.CustomerId,
                order.OrderNumber,
                order.Status,
                order.TotalAmount,
                order.CreatedAt
            }
        });
    }

    /// <summary>
    /// Executes the SearchKnowledgeBase tool by calling <see cref="IKnowledgeService.SearchAsync"/>.
    /// </summary>
    private async Task<AIToolResult> ExecuteSearchKnowledgeBaseAsync(
        AIToolCall toolCall,
        CancellationToken cancellationToken)
    {
        if (!TryReadStringArgument(toolCall.ArgumentsJson, "query", out var query))
        {
            return CreateErrorResult(toolCall, "Argument 'query' must be a non-empty string.");
        }

        try
        {
            var results = await _knowledgeService.SearchAsync(query, cancellationToken: cancellationToken);
            if (results.Count == 0)
            {
                return CreateSuccessResult(toolCall, new
                {
                    success = true,
                    results = Array.Empty<object>(),
                    message = "No relevant company knowledge was found for the query."
                });
            }

            return CreateSuccessResult(toolCall, new
            {
                success = true,
                results = results.Select(result => new
                {
                    documentTitle = result.DocumentTitle,
                    documentSource = result.DocumentSource,
                    content = result.Content,
                    score = result.Score
                })
            });
        }
        catch (ArgumentException exception)
        {
            return CreateErrorResult(toolCall, exception.Message);
        }
    }

    /// <summary>
    /// Reads one integer argument from the model-provided JSON payload.
    /// </summary>
    private static bool TryReadIntArgument(string argumentsJson, string propertyName, out int value)
    {
        value = default;

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().Count() != 1 ||
                !document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
            {
                return true;
            }

        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Reads one string argument from the model-provided JSON payload.
    /// </summary>
    private static bool TryReadStringArgument(string argumentsJson, string propertyName, out string value)
    {
        value = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().Count() != 1 ||
                !document.RootElement.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a successful tool result payload for the model.
    /// </summary>
    private static AIToolResult CreateSuccessResult(AIToolCall toolCall, object payload) =>
        new()
        {
            ToolCallId = toolCall.Id,
            ToolName = toolCall.Name,
            Success = true,
            Content = JsonSerializer.Serialize(payload)
        };

    /// <summary>
    /// Creates an error tool result payload for the model.
    /// </summary>
    private static AIToolResult CreateErrorResult(AIToolCall toolCall, string error) =>
        new()
        {
            ToolCallId = toolCall.Id,
            ToolName = toolCall.Name,
            Success = false,
            Error = error,
            Content = JsonSerializer.Serialize(new
            {
                success = false,
                error
            })
        };
}

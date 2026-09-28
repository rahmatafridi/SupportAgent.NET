using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models.AI;
using SupportAgent.Core.BusinessQuery;
using SupportAgent.Infrastructure.BusinessQuery;

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
        SupportToolDefinitions.SearchKnowledgeBaseToolName,
        SupportToolDefinitions.GetInvoiceToolName, SupportToolDefinitions.GetCustomerInvoicesToolName,
        SupportToolDefinitions.GetCustomerBalanceToolName, SupportToolDefinitions.GetInventoryStatusToolName,
        SupportToolDefinitions.GetSalesSummaryToolName, SupportToolDefinitions.GetLowStockProductsToolName,
        SupportToolDefinitions.QueryBusinessDataToolName
    ];

    private readonly ICustomerService _customerService;
    private readonly IOrderService _orderService;
    private readonly IKnowledgeService _knowledgeService;
    private readonly ILogger<AIToolExecutor>? _logger;
    private readonly IHostEnvironment? _hostEnvironment;
    private readonly IInvoiceService? _invoiceService;
    private readonly IInventoryService? _inventoryService;
    private readonly IBusinessQueryService? _businessQueryService;

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
        IHostEnvironment? hostEnvironment = null,
        IInvoiceService? invoiceService = null,
        IInventoryService? inventoryService = null,
        IBusinessQueryService? businessQueryService = null)
    {
        _customerService = customerService;
        _orderService = orderService;
        _knowledgeService = knowledgeService;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _invoiceService = invoiceService;
        _inventoryService = inventoryService;
        _businessQueryService = businessQueryService;
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
            SupportToolDefinitions.GetInvoiceToolName => await Invoice(toolCall, "invoiceId", (id, ct) => _invoiceService!.GetInvoiceAsync(id, ct), cancellationToken),
            SupportToolDefinitions.GetCustomerInvoicesToolName => await Invoice(toolCall, "customerId", (id, ct) => _invoiceService!.GetCustomerInvoicesAsync(id, ct), cancellationToken),
            SupportToolDefinitions.GetCustomerBalanceToolName => await Invoice(toolCall, "customerId", (id, ct) => _invoiceService!.GetCustomerBalanceAsync(id, ct), cancellationToken),
            SupportToolDefinitions.GetInventoryStatusToolName => await Invoice(toolCall, "productId", (id, ct) => _inventoryService!.GetInventoryAsync(id, ct), cancellationToken),
            SupportToolDefinitions.GetLowStockProductsToolName => await LowStock(toolCall, cancellationToken),
            SupportToolDefinitions.GetSalesSummaryToolName => await Sales(toolCall, cancellationToken),
            SupportToolDefinitions.QueryBusinessDataToolName => await QueryBusinessData(toolCall, cancellationToken),
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

    private async Task<AIToolResult> Invoice<T>(AIToolCall call, string argument, Func<int, CancellationToken, Task<T>> execute, CancellationToken token)
    {
        if (!TryReadIntArgument(call.ArgumentsJson, argument, out var id) || id <= 0) return CreateErrorResult(call, $"Argument '{argument}' must be a valid positive integer.");
        if ((_invoiceService is null && call.Name != SupportToolDefinitions.GetInventoryStatusToolName) || (_inventoryService is null && call.Name == SupportToolDefinitions.GetInventoryStatusToolName)) return CreateErrorResult(call, "Business service is unavailable.");
        var value = await execute(id, token); return value is null ? CreateErrorResult(call, "Requested business record was not found.") : CreateSuccessResult(call, new { success = true, result = value });
    }
    private async Task<AIToolResult> LowStock(AIToolCall call, CancellationToken token) { if (call.ArgumentsJson.Trim() is not ("{}" or "")) return CreateErrorResult(call, "This tool accepts no arguments."); return CreateSuccessResult(call, new { success = true, result = await _inventoryService!.GetLowStockProductsAsync(token) }); }
    private async Task<AIToolResult> Sales(AIToolCall call, CancellationToken token) { try { using var d = JsonDocument.Parse(call.ArgumentsJson); if (d.RootElement.EnumerateObject().Count() != 2 || !DateTime.TryParse(d.RootElement.GetProperty("startDate").GetString(), out var start) || !DateTime.TryParse(d.RootElement.GetProperty("endDate").GetString(), out var end)) return CreateErrorResult(call, "startDate and endDate must be valid dates."); return CreateSuccessResult(call, new { success = true, result = await _invoiceService!.GetSalesSummaryAsync(start.Date, end.Date, token) }); } catch (Exception e) when (e is JsonException or KeyNotFoundException or ArgumentException) { return CreateErrorResult(call, e is ArgumentException ? e.Message : "Invalid sales summary arguments."); } }
    private async Task<AIToolResult> QueryBusinessData(AIToolCall call, CancellationToken token)
    {
        if (!TryReadStringArgument(call.ArgumentsJson, "question", out var question) || question.Length > 1000)
            return CreateErrorResult(call, "Argument 'question' must be a non-empty string of at most 1000 characters.");
        if (_businessQueryService is null) return CreateErrorResult(call, "Business query service is unavailable.");
        try { return CreateSuccessResult(call, new { success = true, result = await _businessQueryService.AskAsync(question, token) }); }
        catch (BusinessQueryException exception) { return CreateErrorResult(call, exception.Message); }
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

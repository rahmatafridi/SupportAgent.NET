using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.BusinessQuery;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.BusinessQuery;

public sealed class BusinessQueryExecutor(SupportAgentDbContext db, IBusinessQueryValidator validator) : IBusinessQueryExecutor
{
    private const int CandidateLimit = 1001;

    public async Task<BusinessQueryResult> ExecuteAsync(BusinessQueryPlan plan, CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(plan);
        if (!validation.IsValid) throw new BusinessQueryException("InvalidPlan", string.Join(" ", validation.Errors));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var watch = Stopwatch.StartNew();
        var rows = await LoadRowsAsync(plan.Entity, timeout.Token);
        IEnumerable<IReadOnlyDictionary<string, object?>> query = rows;
        foreach (var filter in plan.Filters) query = query.Where(row => Matches(row, filter));
        IOrderedEnumerable<IReadOnlyDictionary<string, object?>>? ordered = null;
        if (plan.Aggregations.Count == 0 && plan.GroupBy.Count == 0) foreach (var sort in plan.Sort)
        {
            Func<IReadOnlyDictionary<string, object?>, object?> key = row => row.GetValueOrDefault(sort.Field);
            ordered = ordered is null
                ? sort.Direction == BusinessQuerySortDirection.Ascending ? query.OrderBy(key, ValueComparer.Instance) : query.OrderByDescending(key, ValueComparer.Instance)
                : sort.Direction == BusinessQuerySortDirection.Ascending ? ordered.ThenBy(key, ValueComparer.Instance) : ordered.ThenByDescending(key, ValueComparer.Instance);
        }
        if (ordered is not null) query = ordered;
        var projected = plan.Aggregations.Count > 0 || plan.GroupBy.Count > 0
            ? Aggregate(query, plan)
            : Project(query, plan.Select);
        if (plan.Aggregations.Count > 0 || plan.GroupBy.Count > 0)
        {
            IOrderedEnumerable<IReadOnlyDictionary<string, object?>>? aggregateOrder = null;
            foreach (var sort in plan.Sort)
            {
                Func<IReadOnlyDictionary<string, object?>, object?> key = row => row.GetValueOrDefault(sort.Field);
                aggregateOrder = aggregateOrder is null
                    ? sort.Direction == BusinessQuerySortDirection.Ascending ? projected.OrderBy(key, ValueComparer.Instance) : projected.OrderByDescending(key, ValueComparer.Instance)
                    : sort.Direction == BusinessQuerySortDirection.Ascending ? aggregateOrder.ThenBy(key, ValueComparer.Instance) : aggregateOrder.ThenByDescending(key, ValueComparer.Instance);
            }
            if (aggregateOrder is not null) projected = aggregateOrder;
        }
        var materialized = projected.Take(plan.Limit + 1).ToList();
        var truncated = materialized.Count > plan.Limit || rows.Count == CandidateLimit;
        if (materialized.Count > plan.Limit) materialized.RemoveAt(materialized.Count - 1);
        watch.Stop();
        var columns = materialized.FirstOrDefault()?.Keys.ToList()
            ?? plan.GroupBy.Concat(plan.Aggregations.Select(x => x.Alias)).ToList();
        return new(columns, materialized, materialized.Count, truncated, watch.ElapsedMilliseconds);
    }

    private async Task<List<IReadOnlyDictionary<string, object?>>> LoadRowsAsync(string entity, CancellationToken ct)
    {
        switch (entity.ToLowerInvariant())
        {
            case "customers": return (await db.Customers.AsNoTracking().Take(CandidateLimit).ToListAsync(ct)).Select(x => Row(("Id",x.Id),("FirstName",x.FirstName),("LastName",x.LastName),("Name",$"{x.FirstName} {x.LastName}".Trim()),("Email",x.Email),("CreatedAt",x.CreatedAt))).ToList();
            case "products": return (await db.Products.AsNoTracking().Include(x=>x.Inventory).Take(CandidateLimit).ToListAsync(ct)).Select(x => {var low=x.Inventory is not null&&BusinessSemantics.IsLowStock(x.Inventory.QuantityOnHand,x.Inventory.ReorderLevel);return Row(("Id",x.Id),("Sku",x.Sku),("Name",x.Name),("UnitPrice",x.UnitPrice),("IsActive",x.IsActive),("Inventory.QuantityOnHand",x.Inventory?.QuantityOnHand),("Inventory.ReorderLevel",x.Inventory?.ReorderLevel),("Inventory.IsLowStock",low),("CreatedAt",x.CreatedAt));}).ToList();
            case "orders": return (await db.Orders.AsNoTracking().Include(x=>x.Customer).Take(CandidateLimit).ToListAsync(ct)).Select(x => Row(("Id",x.Id),("OrderNumber",x.OrderNumber),("CustomerId",x.CustomerId),("Customer.Name",$"{x.Customer.FirstName} {x.Customer.LastName}".Trim()),("Status",x.Status),("TotalAmount",x.TotalAmount),("CreatedAt",x.CreatedAt))).ToList();
            case "orderitems": return (await db.OrderItems.AsNoTracking().Include(x=>x.Product).Take(CandidateLimit).ToListAsync(ct)).Select(x => Row(("Id",x.Id),("OrderId",x.OrderId),("ProductId",x.ProductId),("Product.Name",x.Product?.Name),("Description",x.Description),("Quantity",x.Quantity),("UnitPrice",x.UnitPrice),("LineTotal",x.LineTotal))).ToList();
            case "invoices": return (await db.Invoices.AsNoTracking().Include(x=>x.Customer).Include(x=>x.Payments).Take(CandidateLimit).ToListAsync(ct)).Select(x => {var paid=x.Payments.Sum(p=>p.Amount);var outstanding=Math.Max(0,x.TotalAmount-paid);return Row(("Id",x.Id),("InvoiceNumber",x.InvoiceNumber),("CustomerId",x.CustomerId),("Customer.Name",$"{x.Customer.FirstName} {x.Customer.LastName}".Trim()),("Status",x.Status),("IssueDate",x.IssueDate),("DueDate",x.DueDate),("TotalAmount",x.TotalAmount),("PaidAmount",paid),("OutstandingBalance",outstanding),("IsOverdue",BusinessSemantics.IsInvoiceOverdue(x.DueDate,outstanding,x.Status,DateTime.UtcNow.Date)),("IsFullyPaid",BusinessSemantics.IsInvoiceFullyPaid(outstanding,x.Status)));}).ToList();
            case "invoiceitems": return (await db.InvoiceItems.AsNoTracking().Include(x=>x.Product).Take(CandidateLimit).ToListAsync(ct)).Select(x => Row(("Id",x.Id),("InvoiceId",x.InvoiceId),("ProductId",x.ProductId),("Product.Name",x.Product?.Name),("Description",x.Description),("Quantity",x.Quantity),("UnitPrice",x.UnitPrice),("LineTotal",x.LineTotal))).ToList();
            case "payments": return (await db.Payments.AsNoTracking().Include(x=>x.Invoice).Take(CandidateLimit).ToListAsync(ct)).Select(x => Row(("Id",x.Id),("InvoiceId",x.InvoiceId),("Invoice.InvoiceNumber",x.Invoice.InvoiceNumber),("Amount",x.Amount),("PaymentDate",x.PaymentDate),("Method",x.Method))).ToList();
            case "inventoryitems": return (await db.InventoryItems.AsNoTracking().Include(x=>x.Product).Take(CandidateLimit).ToListAsync(ct)).Select(x => {var low=BusinessSemantics.IsLowStock(x.QuantityOnHand,x.ReorderLevel);return Row(("Id",x.Id),("ProductId",x.ProductId),("Product.Sku",x.Product.Sku),("Product.Name",x.Product.Name),("QuantityOnHand",x.QuantityOnHand),("ReorderLevel",x.ReorderLevel),("LowStock",low),("IsLowStock",low),("UpdatedAt",x.UpdatedAt));}).ToList();
            default: throw new BusinessQueryException("UnknownEntity", "The requested entity is not available.");
        }
    }

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Project(IEnumerable<IReadOnlyDictionary<string, object?>> rows, IReadOnlyList<string> fields)
    {
        foreach (var row in rows)
        {
            var selected = fields.Count == 0 ? row.Keys : fields;
            yield return selected.ToDictionary(field => field, field => row.GetValueOrDefault(field), StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Aggregate(IEnumerable<IReadOnlyDictionary<string, object?>> rows, BusinessQueryPlan plan)
    {
        var groups = plan.GroupBy.Count == 0
            ? new[] { new { Key = Array.Empty<object?>(), Items = rows.ToList() } }
            : rows.GroupBy(row => string.Join("\u001f", plan.GroupBy.Select(field => row.GetValueOrDefault(field)?.ToString())))
                .Select(group => new { Key = plan.GroupBy.Select(field => group.First().GetValueOrDefault(field)).ToArray(), Items = group.ToList() });
        foreach (var group in groups)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i=0;i<plan.GroupBy.Count;i++) result[plan.GroupBy[i]]=group.Key[i];
            foreach (var aggregation in plan.Aggregations)
            {
                var values = string.IsNullOrWhiteSpace(aggregation.Field) ? [] : group.Items.Select(x=>x.GetValueOrDefault(aggregation.Field)).Where(x=>x is not null).Select(Convert.ToDecimal).ToList();
                result[aggregation.Alias] = aggregation.Function switch
                {
                    BusinessAggregationFunction.Count => group.Items.Count,
                    BusinessAggregationFunction.Sum => values.Sum(),
                    BusinessAggregationFunction.Average => values.Count == 0 ? 0 : values.Average(),
                    BusinessAggregationFunction.Min => values.Count == 0 ? null : values.Min(),
                    BusinessAggregationFunction.Max => values.Count == 0 ? null : values.Max(),
                    _ => null
                };
            }
            yield return result;
        }
    }

    private static bool Matches(IReadOnlyDictionary<string, object?> row, BusinessQueryFilter filter)
    {
        var actual = row.GetValueOrDefault(filter.Field);
        if (filter.Operator == BusinessQueryOperator.IsNull) return actual is null;
        if (filter.Operator == BusinessQueryOperator.IsNotNull) return actual is not null;
        if (actual is null) return false;
        if (filter.Value is null) return false;
        var value = filter.Value.Value;
        if (filter.Operator == BusinessQueryOperator.In && value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Any(item => Compare(actual, Scalar(item)) == 0);
        if (filter.Operator == BusinessQueryOperator.Between && value.ValueKind == JsonValueKind.Object)
            return Compare(actual, Scalar(value.GetProperty("start"))) >= 0 && Compare(actual, Scalar(value.GetProperty("end"))) <= 0;
        var expected = Scalar(value);
        return filter.Operator switch
        {
            BusinessQueryOperator.Equals => Compare(actual, expected) == 0,
            BusinessQueryOperator.NotEquals => Compare(actual, expected) != 0,
            BusinessQueryOperator.GreaterThan => Compare(actual, expected) > 0,
            BusinessQueryOperator.GreaterThanOrEqual => Compare(actual, expected) >= 0,
            BusinessQueryOperator.LessThan => Compare(actual, expected) < 0,
            BusinessQueryOperator.LessThanOrEqual => Compare(actual, expected) <= 0,
            BusinessQueryOperator.Contains => actual?.ToString()?.Contains(expected?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) == true,
            BusinessQueryOperator.StartsWith => actual?.ToString()?.StartsWith(expected?.ToString() ?? "", StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };
    }

    private static object? Scalar(JsonElement value)
    {
        // Some providers wrap schema values as {"type":"Date","value":"..."}.
        // Only unwrap the data value; never interpret the provider-supplied type.
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var wrapped))
            return Scalar(wrapped);
        return value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number when value.TryGetDecimal(out var d) => d, JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => value.ToString() };
    }
    private static int Compare(object? left, object? right)
    {
        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;
        if (left is DateTime date && DateTime.TryParse(right.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var otherDate)) return date.CompareTo(otherDate);
        if (left is decimal or int or long or double && decimal.TryParse(right.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number)) return Convert.ToDecimal(left).CompareTo(number);
        if (left is bool boolean && bool.TryParse(right.ToString(), out var otherBoolean)) return boolean.CompareTo(otherBoolean);
        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ValueComparer : IComparer<object?> { public static readonly ValueComparer Instance=new(); public int Compare(object? x, object? y)=>BusinessQueryExecutor.Compare(x,y); }
}

public sealed class BusinessQueryException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }

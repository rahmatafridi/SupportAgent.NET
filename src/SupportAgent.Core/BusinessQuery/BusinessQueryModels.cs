using System.Text.Json;
using System.Text.Json.Serialization;

namespace SupportAgent.Core.BusinessQuery;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessQueryOperator { Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Contains, StartsWith, In, IsNull, IsNotNull, Between }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessQuerySortDirection { Ascending, Descending }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessAggregationFunction { Count, Sum, Average, Min, Max }

public sealed record BusinessColumnSchema(string Name, string DisplayName, string Description, string DataType, bool InternalTenantKey = false, bool Computed = false, IReadOnlyList<string>? TrueWhenQuestionContains = null, IReadOnlyList<string>? Concepts = null);
public sealed record BusinessRelationshipSchema(string FromEntity, string FromField, string ToEntity, string ToField, string? Name = null, string? Description = null, IReadOnlyList<string>? Concepts = null, string RelationshipType = "ManyToOne");
public sealed record BusinessTableSchema(string Name, string DisplayName, string Description, IReadOnlyList<string> Concepts, IReadOnlyList<BusinessColumnSchema> Columns, string? DefaultRecencyField = null);
public sealed record BusinessSchema(IReadOnlyList<BusinessTableSchema> Entities, IReadOnlyList<BusinessRelationshipSchema> Relationships);

public sealed class BusinessQueryPlan
{
    public string Entity { get; set; } = "";
    public List<string> Select { get; set; } = [];
    public List<BusinessQueryFilter> Filters { get; set; } = [];
    public List<BusinessQuerySort> Sort { get; set; } = [];
    public List<BusinessQueryAggregation> Aggregations { get; set; } = [];
    public List<string> GroupBy { get; set; } = [];
    public int Limit { get; set; } = 25;
    public bool RequiresClarification { get; set; }
    public string? ClarificationQuestion { get; set; }
}
public sealed record BusinessQueryFilter(string Field, BusinessQueryOperator Operator, JsonElement? Value);
public sealed record BusinessQuerySort(string Field, BusinessQuerySortDirection Direction);
public sealed record BusinessQueryAggregation(BusinessAggregationFunction Function, string? Field, string Alias, bool Distinct = false);
public sealed record BusinessQueryValidationResult(bool IsValid, IReadOnlyList<string> Errors);
public sealed record BusinessQueryResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, int RowCount, bool Truncated, long DurationMs);
public sealed record BusinessQueryColumnDisplay(string Name, string DisplayName, string DataType);
public sealed record BusinessQueryResponse(string Answer, BusinessQueryPlan Plan, BusinessQueryResult Result, IReadOnlyList<BusinessQueryColumnDisplay> DisplayColumns, string Provider, string Model, int InputTokens, int OutputTokens, IReadOnlyList<string>? Relationships = null, string? DataSourceName = null, string? ScopeName = null, IReadOnlyList<string>? SelectedEntities = null);
public sealed record BusinessQueryPlanningResult(BusinessQueryPlan Plan, string Provider, string Model, int InputTokens, int OutputTokens);

public class BusinessQueryAudit
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ApplicationUserId { get; set; }
    public string Question { get; set; } = "";
    public string QueryPlanJson { get; set; } = "";
    public int RowCount { get; set; }
    public long DurationMs { get; set; }
    public bool Succeeded { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? DataSourceId { get; set; }
    public string? DataSourceName { get; set; }
    public string? DataSourceProvider { get; set; }
}

public static class BusinessSemantics
{
    public static bool IsInvoiceOverdue(DateTime? dueDate, decimal outstandingBalance, string status, DateTime today) =>
        dueDate.HasValue && dueDate.Value.Date < today.Date && outstandingBalance > 0 &&
        !status.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
        !status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
    public static bool IsInvoiceFullyPaid(decimal outstandingBalance, string status) =>
        outstandingBalance <= 0 && !status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
    public static bool IsLowStock(decimal quantityOnHand, decimal reorderLevel) => quantityOnHand <= reorderLevel;
}

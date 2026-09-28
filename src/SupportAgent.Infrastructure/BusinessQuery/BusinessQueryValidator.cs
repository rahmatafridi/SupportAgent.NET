using SupportAgent.Core.BusinessQuery;

namespace SupportAgent.Infrastructure.BusinessQuery;

public sealed class BusinessQueryValidator(IBusinessSchemaService schemas) : IBusinessQueryValidator
{
    public BusinessQueryValidationResult Validate(BusinessQueryPlan plan)
    {
        var errors = new List<string>();
        var schema = schemas.GetSchema();
        var entity = schema.Entities.FirstOrDefault(x => x.Name.Equals(plan.Entity, StringComparison.OrdinalIgnoreCase));
        if (entity is null) return new(false, ["The requested entity is not available for AI querying."]);
        BusinessColumnSchema? Field(string field, string use)
        {
            if (field.Equals("OrganizationId", StringComparison.OrdinalIgnoreCase)){errors.Add("OrganizationId is server-controlled.");return null;}
            var direct=entity.Columns.FirstOrDefault(x=>!x.InternalTenantKey&&x.Name.Equals(field,StringComparison.OrdinalIgnoreCase));if(direct is not null)return direct;
            var parts=field.Split('.',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);if(parts.Length<2||parts.Length>4){errors.Add($"Unknown {use} field '{field}'.");return null;}
            var current=entity;
            for(var i=0;i<parts.Length-1;i++)
            {
                var matches=schema.Relationships.Where(x=>x.FromEntity.Equals(current.Name,StringComparison.OrdinalIgnoreCase)&&((x.Name??x.ToEntity).Equals(parts[i],StringComparison.OrdinalIgnoreCase)||x.ToEntity.Equals(parts[i],StringComparison.OrdinalIgnoreCase))).ToList();
                if(matches.Count==0){errors.Add($"Unknown or unapproved relationship '{parts[i]}' in {use} field '{field}'.");return null;}
                if(matches.Count>1){errors.Add($"Relationship '{parts[i]}' is ambiguous. Use a specific relationship name.");return null;}
                current=schema.Entities.FirstOrDefault(x=>x.Name.Equals(matches[0].ToEntity,StringComparison.OrdinalIgnoreCase));if(current is null){errors.Add("Relationship target is unavailable in the selected data scope.");return null;}
            }
            var column=current.Columns.FirstOrDefault(x=>!x.InternalTenantKey&&x.Name.Equals(parts[^1],StringComparison.OrdinalIgnoreCase));if(column is null)errors.Add($"Unknown {use} field '{field}'.");return column;
        }
        foreach (var field in plan.Select) Field(field, "selected");
        foreach (var filter in plan.Filters) { Field(filter.Field, "filter"); if (!Enum.IsDefined(filter.Operator)) errors.Add("Unsupported filter operator."); }
        var aggregationAliases = plan.Aggregations.Select(x => x.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var sort in plan.Sort) if (!aggregationAliases.Contains(sort.Field)) Field(sort.Field, "sort");
        foreach (var group in plan.GroupBy) Field(group, "grouping");
        foreach (var aggregation in plan.Aggregations)
        {
            if (!Enum.IsDefined(aggregation.Function)) errors.Add("Unsupported aggregation function.");
            if (string.IsNullOrWhiteSpace(aggregation.Alias) || aggregation.Alias.Length > 60) errors.Add("Aggregation alias is invalid.");
            if (aggregation.Function != BusinessAggregationFunction.Count && string.IsNullOrWhiteSpace(aggregation.Field)) errors.Add($"{aggregation.Function} requires a field.");
            if (!string.IsNullOrWhiteSpace(aggregation.Field)) Field(aggregation.Field, "aggregation");
            var column = string.IsNullOrWhiteSpace(aggregation.Field)?null:Field(aggregation.Field, "aggregation type");
            if (aggregation.Function is BusinessAggregationFunction.Sum or BusinessAggregationFunction.Average &&
                !(column?.DataType.Equals("Money",StringComparison.OrdinalIgnoreCase)==true || column?.DataType.Equals("Number",StringComparison.OrdinalIgnoreCase)==true || column?.DataType.Equals("Decimal",StringComparison.OrdinalIgnoreCase)==true || column?.DataType.Equals("Integer",StringComparison.OrdinalIgnoreCase)==true))
                errors.Add($"{aggregation.Function} requires a numeric field.");
        }
        if(plan.Aggregations.Count>0&&plan.Select.Any(field=>!plan.GroupBy.Contains(field,StringComparer.OrdinalIgnoreCase)))
            errors.Add("Selected non-aggregate fields must be included in groupBy when aggregations are used.");
        if (plan.Limit is < 1 or > 100) errors.Add("Row limit must be between 1 and 100.");
        if (plan.Filters.Count > 10 || plan.Sort.Count > 3 || plan.GroupBy.Count > 2 || plan.Aggregations.Count > 5 || plan.Select.Count > 12)
            errors.Add("The query plan is too complex.");
        var relationshipDepth = plan.Select.Concat(plan.Filters.Select(x => x.Field)).Concat(plan.Sort.Select(x => x.Field)).Concat(plan.GroupBy).Concat(plan.Aggregations.Where(x=>x.Field is not null).Select(x=>x.Field!)).Select(x=>Math.Max(0,x.Count(c=>c=='.'))).DefaultIfEmpty().Max();
        if (relationshipDepth > 3) errors.Add("Relationship depth cannot exceed 3 joins.");
        return new(errors.Count == 0, errors);
    }
}

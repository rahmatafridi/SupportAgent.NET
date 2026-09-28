using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportAgent.Core.BusinessQuery;
using SupportAgent.Core.ExternalData;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.BusinessQuery;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.ExternalData;

public sealed class ConfigurationExternalDataSourceSecretProvider(IConfiguration configuration) : IExternalDataSourceSecretProvider
{
    public string GetConnectionString(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Any(c => !char.IsLetterOrDigit(c) && c != '_')) throw new ExternalDataException("SecretUnavailable", "The external data source secret reference is invalid.");
        var value = configuration[$"ExternalDataSources:{secretKey}:ConnectionString"] ?? configuration[$"SUPPORTAGENT_DATASOURCE_{secretKey}"] ?? Environment.GetEnvironmentVariable($"SUPPORTAGENT_DATASOURCE_{secretKey}");
        return string.IsNullOrWhiteSpace(value) ? throw new ExternalDataException("SecretUnavailable", "The external data source secret is not configured.") : value;
    }
}

public sealed class SqlServerExternalDataSourceConnector(ILogger<SqlServerExternalDataSourceConnector> logger) : IExternalDataSourceConnector
{
    public ExternalDataSourceProvider Provider => ExternalDataSourceProvider.SqlServer;
    private static SqlConnection Open(string cs) => new(cs);
    public async Task<ExternalConnectionTestResult> TestAsync(string cs, CancellationToken token = default) { var sw = Stopwatch.StartNew(); try { await using var c = Open(cs); await c.OpenAsync(token); await using var cmd = new SqlCommand("SELECT DB_NAME()", c) { CommandTimeout = 10 }; var db = (string?)await cmd.ExecuteScalarAsync(token); return new(true, "SqlServer", db, sw.ElapsedMilliseconds); } catch (Exception ex) { logger.LogWarning(ex, "External SQL Server connection test failed."); return new(false, "SqlServer", null, sw.ElapsedMilliseconds, "Connection failed. Verify the configured secret, network, database, and read-only permissions."); } }
    public async Task<IReadOnlyList<ExternalDiscoveredObject>> DiscoverAsync(string cs, CancellationToken token = default)
    {
        const string sql = """
  SELECT t.TABLE_SCHEMA,t.TABLE_NAME,t.TABLE_TYPE,c.COLUMN_NAME,c.DATA_TYPE,c.IS_NULLABLE,
   CAST(CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END AS bit)
  FROM INFORMATION_SCHEMA.TABLES t JOIN INFORMATION_SCHEMA.COLUMNS c ON c.TABLE_SCHEMA=t.TABLE_SCHEMA AND c.TABLE_NAME=t.TABLE_NAME
  LEFT JOIN (SELECT ku.TABLE_SCHEMA,ku.TABLE_NAME,ku.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON ku.CONSTRAINT_NAME=tc.CONSTRAINT_NAME AND ku.TABLE_SCHEMA=tc.TABLE_SCHEMA WHERE tc.CONSTRAINT_TYPE='PRIMARY KEY') k ON k.TABLE_SCHEMA=c.TABLE_SCHEMA AND k.TABLE_NAME=c.TABLE_NAME AND k.COLUMN_NAME=c.COLUMN_NAME
  WHERE t.TABLE_TYPE IN ('BASE TABLE','VIEW') ORDER BY t.TABLE_SCHEMA,t.TABLE_NAME,c.ORDINAL_POSITION
  """;
        await using var c = Open(cs); await c.OpenAsync(token); await using var cmd = new SqlCommand(sql, c) { CommandTimeout = 10 }; await using var r = await cmd.ExecuteReaderAsync(token); var map = new Dictionary<string, (string S, string N, string T, List<ExternalDiscoveredColumn> C)>();
        while (await r.ReadAsync(token)) { var s = r.GetString(0); var n = r.GetString(1); var key = s + "." + n; if (!map.TryGetValue(key, out var x)) x = (s, n, r.GetString(2), []); var type = r.GetString(4); x.C.Add(new(r.GetString(3), type, r.GetString(5) == "YES", r.GetBoolean(6), Supported(type))); map[key] = x; }
        await r.DisposeAsync();
        const string fkSql = """
  SELECT fk.name,OBJECT_SCHEMA_NAME(fk.parent_object_id),OBJECT_NAME(fk.parent_object_id),pc.name,
         OBJECT_SCHEMA_NAME(fk.referenced_object_id),OBJECT_NAME(fk.referenced_object_id),rc.name
  FROM sys.foreign_keys fk
  JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
  JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
  JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
  """;
        await using var fkCmd = new SqlCommand(fkSql, c) { CommandTimeout = 10 }; await using var fr = await fkCmd.ExecuteReaderAsync(token); var fks = new List<ExternalDiscoveredForeignKey>();
        while (await fr.ReadAsync(token)) fks.Add(new(fr.GetString(0), fr.GetString(1), fr.GetString(2), fr.GetString(3), fr.GetString(4), fr.GetString(5), fr.GetString(6)));
        return map.Values.Select(x => new ExternalDiscoveredObject(x.S, x.N, x.T, x.C.Select(col => col with { ForeignKey = fks.Any(f => f.FromSchema == x.S && f.FromObject == x.N && f.FromColumn == col.Name) }).ToList(), fks.Where(f => f.FromSchema == x.S && f.FromObject == x.N).ToList())).ToList();
    }
    static bool Supported(string t) => new[] { "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "money", "smallmoney", "bit", "nvarchar", "varchar", "nchar", "char", "date", "datetime", "datetime2", "smalldatetime", "uniqueidentifier" }.Contains(t, StringComparer.OrdinalIgnoreCase);
    public async Task<BusinessQueryResult> ExecuteAsync(string cs, ExternalCompiledQuery query, int limit, CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew(); try { await using var c = Open(cs); await c.OpenAsync(token); await using var cmd = new SqlCommand(query.CommandText, c) { CommandTimeout = 10 }; foreach (var p in query.Parameters) cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value); await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult, token); var columns = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToList(); var rows = new List<IReadOnlyDictionary<string, object?>>(); var bytes = 0; while (await r.ReadAsync(token)) { var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase); for (var i = 0; i < r.FieldCount; i++) { var v = await r.IsDBNullAsync(i, token) ? null : r.GetValue(i); row[columns[i]] = v; bytes += v?.ToString()?.Length ?? 0; } if (bytes > 262144) throw new ExternalDataException("ResultTooLarge", "The external query result was too large."); rows.Add(row); } var returned = rows.Take(limit).ToList(); return new BusinessQueryResult(columns, returned, returned.Count, rows.Count > limit, sw.ElapsedMilliseconds); }
        catch (OperationCanceledException) { throw new ExternalDataException("ExternalTimeout", "The external data query took too long."); }
        catch (ExternalDataException) { throw; }
        catch (SqlException ex) { logger.LogWarning(ex, "External query failed. SQL template: {Sql}", query.CommandText); throw new ExternalDataException(ex.Number is 207 or 208 ? "SchemaDrift" : "ExternalQueryFailed", ex.Number is 207 or 208 ? "External data source schema has changed. An administrator should review the mapping." : "The external data query could not be completed."); }
    }
}

public sealed class ExternalSchemaDiscoveryService(SupportAgentDbContext db, IExternalDataSourceSecretProvider secrets, IEnumerable<IExternalDataSourceConnector> connectors) : IExternalSchemaDiscoveryService
{ public async Task<IReadOnlyList<ExternalDiscoveredObject>> DiscoverAsync(Guid id, CancellationToken token = default) { var s = await db.ExternalDataSources.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new ExternalDataException("SourceNotFound", "External data source was not found."); return await connectors.Single(x => x.Provider == s.Provider).DiscoverAsync(secrets.GetConnectionString(s.SecretKey), token); } }

public sealed class ExternalBusinessSchemaProvider(SupportAgentDbContext db) : IExternalBusinessSchemaProvider
{
    public async Task<BusinessSchema> GetSchemaAsync(Guid id, Guid? scopeId = null, CancellationToken token = default) { var query = db.ExternalEntityMappings.AsNoTracking().Where(x => x.ExternalDataSourceId == id && x.IsEnabled && x.IsValid); if (scopeId.HasValue) { var scope = await db.ExternalDataScopes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == scopeId && x.ExternalDataSourceId == id && x.IsEnabled, token) ?? throw new ExternalDataException("ScopeUnavailable", "The selected data scope is unavailable."); var ids = db.ExternalDataScopeEntities.Where(x => x.ExternalDataScopeId == scope.Id).Select(x => x.ExternalEntityMappingId); query = query.Where(x => ids.Contains(x.Id)); } var entities = await query.Include(x => x.Fields).ToListAsync(token); var tables = entities.Select(e => new BusinessTableSchema(e.BusinessEntityName, e.DisplayName, e.Description, Split(e.Concepts), e.Fields.Where(f => f.IsQueryable && !f.IsSensitive).Select(f => new BusinessColumnSchema(f.BusinessFieldName, f.DisplayName, f.Description, f.DataType, Concepts: Split(f.Concepts))).ToList(), e.DefaultRecencyField)).Where(x => x.Columns.Count > 0).ToList(); var names = tables.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase); var rel = await db.ExternalRelationshipMappings.AsNoTracking().Where(x => x.ExternalDataSourceId == id && x.IsEnabled && names.Contains(x.FromEntity) && names.Contains(x.ToEntity)).Select(x => new BusinessRelationshipSchema(x.FromEntity, x.FromField, x.ToEntity, x.ToField, x.Name, x.Description, Split(x.Concepts), x.RelationshipType)).ToListAsync(token); return new(tables, rel); }
    static string[] Split(string x) => x.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed class SqlServerBusinessQueryCompiler : IExternalBusinessQueryCompiler
{
    public ExternalCompiledQuery Compile(BusinessQueryPlan p, BusinessSchema schema, IReadOnlyList<ExternalEntityMapping> entities, IReadOnlyList<ExternalRelationshipMapping> relationships)
    {
        var main = entities.SingleOrDefault(x => x.IsEnabled && x.IsValid && x.BusinessEntityName.Equals(p.Entity, StringComparison.OrdinalIgnoreCase)) ?? throw new ExternalDataException("SchemaDrift", "The external data mapping has changed and requires administrator review.");
        var parameters = new List<ExternalQueryParameter>(); var joins = new Dictionary<string, Join>(StringComparer.OrdinalIgnoreCase);
        string Field(string logical)
        {
            var parts = logical.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); if (parts.Length == 0 || parts.Length > 4) throw new ExternalDataException("InvalidPlan", "Relationship depth exceeds the safe maximum of 3.");
            var current = main; var alias = "t0"; var path = "";
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var segment = parts[i]; var matches = relationships.Where(x => x.IsEnabled && x.ExternalDataSourceId == main.ExternalDataSourceId && x.FromEntity.Equals(current.BusinessEntityName, StringComparison.OrdinalIgnoreCase) && ((string.IsNullOrWhiteSpace(x.Name) ? x.ToEntity : x.Name).Equals(segment, StringComparison.OrdinalIgnoreCase) || x.ToEntity.Equals(segment, StringComparison.OrdinalIgnoreCase))).ToList();
                if (matches.Count != 1) throw new ExternalDataException("InvalidPlan", matches.Count == 0 ? $"Unapproved relationship '{segment}'." : $"Relationship '{segment}' is ambiguous; use its logical relationship name.");
                var rel = matches[0]; path = path.Length == 0 ? segment : path + "." + segment;
                if (!joins.TryGetValue(path, out var join))
                {
                    var target = entities.SingleOrDefault(x => x.ExternalDataSourceId == main.ExternalDataSourceId && x.IsEnabled && x.IsValid && x.BusinessEntityName.Equals(rel.ToEntity, StringComparison.OrdinalIgnoreCase)) ?? throw new ExternalDataException("SchemaDrift", "The external data mapping has changed and requires administrator review.");
                    var left = current.Fields.SingleOrDefault(x => x.IsQueryable && !x.IsSensitive && x.BusinessFieldName.Equals(rel.FromField, StringComparison.OrdinalIgnoreCase)); var right = target.Fields.SingleOrDefault(x => x.IsQueryable && !x.IsSensitive && x.BusinessFieldName.Equals(rel.ToField, StringComparison.OrdinalIgnoreCase)); if (left is null || right is null) throw new ExternalDataException("InvalidPlan", "Relationship fields are not approved for querying.");
                    join = new("t" + (joins.Count + 1), target, alias, left, right, rel); joins[path] = join;
                }
                current = join.Entity; alias = join.Alias;
            }
            var mapped = current.Fields.SingleOrDefault(x => x.IsQueryable && !x.IsSensitive && x.BusinessFieldName.Equals(parts[^1], StringComparison.OrdinalIgnoreCase)) ?? throw new ExternalDataException("InvalidPlan", $"Unapproved external field '{logical}'."); return $"[{alias}].[{Q(mapped.ExternalColumnName)}]";
        }
        string Param(object? value) { var n = "@p" + parameters.Count; parameters.Add(new(n, value)); return n; }
        string Value(JsonElement? e) => Param(e is null ? null : Scalar(e.Value));
        var outputs = new List<string>(); foreach (var x in p.Select) outputs.Add($"{Field(x)} AS [{Q(x)}]"); foreach (var a in p.Aggregations) { var arg = a.Function == BusinessAggregationFunction.Count && string.IsNullOrWhiteSpace(a.Field) ? "*" : Field(a.Field!); var distinct = a.Distinct ? "DISTINCT " : ""; outputs.Add($"{a.Function.ToString().ToUpperInvariant()}({distinct}{arg}) AS [{Q(a.Alias)}]"); }
        if (outputs.Count == 0) throw new ExternalDataException("InvalidPlan", "At least one approved field is required.");
        var where = p.Filters.Select(f => f.Operator switch { BusinessQueryOperator.Equals => $"{Field(f.Field)} = {Value(f.Value)}", BusinessQueryOperator.NotEquals => $"{Field(f.Field)} <> {Value(f.Value)}", BusinessQueryOperator.GreaterThan => $"{Field(f.Field)} > {Value(f.Value)}", BusinessQueryOperator.GreaterThanOrEqual => $"{Field(f.Field)} >= {Value(f.Value)}", BusinessQueryOperator.LessThan => $"{Field(f.Field)} < {Value(f.Value)}", BusinessQueryOperator.LessThanOrEqual => $"{Field(f.Field)} <= {Value(f.Value)}", BusinessQueryOperator.Contains => $"{Field(f.Field)} LIKE {Param("%" + Scalar(f.Value!.Value) + "%")}", BusinessQueryOperator.StartsWith => $"{Field(f.Field)} LIKE {Param(Scalar(f.Value!.Value) + "%")}", BusinessQueryOperator.In => $"{Field(f.Field)} IN ({string.Join(",", f.Value!.Value.EnumerateArray().Select(x => Param(Scalar(x))))})", BusinessQueryOperator.IsNull => $"{Field(f.Field)} IS NULL", BusinessQueryOperator.IsNotNull => $"{Field(f.Field)} IS NOT NULL", BusinessQueryOperator.Between => $"{Field(f.Field)} BETWEEN {Param(Scalar(f.Value!.Value.GetProperty("start")))} AND {Param(Scalar(f.Value!.Value.GetProperty("end")))}", _ => throw new ExternalDataException("InvalidPlan", "Unsupported external filter.") }).ToList();
        var groups = p.GroupBy.Select(Field).ToList(); string SortField(string name) => p.Aggregations.Any(a => a.Alias.Equals(name, StringComparison.OrdinalIgnoreCase)) ? $"[{Q(name)}]" : Field(name); var sorts = p.Sort.Select(s => $"{SortField(s.Field)} {(s.Direction == BusinessQuerySortDirection.Descending ? "DESC" : "ASC")}").ToList();
        var joinSql = string.Join(" ", joins.Values.Select(j => $"LEFT JOIN [{Q(j.Entity.ExternalSchemaName)}].[{Q(j.Entity.ExternalTableName)}] [{j.Alias}] ON [{j.ParentAlias}].[{Q(j.Left.ExternalColumnName)}] = [{j.Alias}].[{Q(j.Right.ExternalColumnName)}]"));
        var sql = $"SELECT TOP ({Math.Min(p.Limit + 1, 101)}) {string.Join(", ", outputs)} FROM [{Q(main.ExternalSchemaName)}].[{Q(main.ExternalTableName)}] [t0] {joinSql}" + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") + (groups.Count > 0 ? " GROUP BY " + string.Join(", ", groups) : "") + (sorts.Count > 0 ? " ORDER BY " + string.Join(", ", sorts) : ""); return new(sql, parameters);
    }
    sealed record Join(string Alias, ExternalEntityMapping Entity, string ParentAlias, ExternalFieldMapping Left, ExternalFieldMapping Right, ExternalRelationshipMapping Relationship);
    static string Q(string x) => x.Replace("]", "]]", StringComparison.Ordinal); static object? Scalar(JsonElement e) { if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("value", out var wrapped)) return Scalar(wrapped); return e.ValueKind switch { JsonValueKind.String => e.GetString(), JsonValueKind.Number when e.TryGetDecimal(out var d) => d, JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null, _ => e.ToString() }; }
}
public sealed class ExternalDataException(string code, string message) : InvalidOperationException(message) { public string Code { get; } = code; }
public static class ExternalExplanationSafety
{
    public static string SafeAnswer(string? answer, BusinessQueryPlan plan, BusinessQueryResult result, string entityDisplay)
    {
        var text = answer?.Trim() ?? ""; if (!LooksLikeSql(text)) return string.IsNullOrWhiteSpace(text) ? Fallback(plan, result, entityDisplay) : text; return Fallback(plan, result, entityDisplay);
    }
    static bool LooksLikeSql(string text) { var normalized = text.TrimStart('`', ' ', '\r', '\n', '\t'); return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^(select|with|insert|update|delete|drop|alter|merge|truncate|exec(?:ute)?)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(normalized, @"\b(from|join)\s+\[?[A-Za-z_]", System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
    static string Fallback(BusinessQueryPlan plan, BusinessQueryResult result, string entityDisplay) { var noun = string.IsNullOrWhiteSpace(entityDisplay) ? plan.Entity : entityDisplay; var sort = plan.Sort.FirstOrDefault(); var ordering = sort is null ? "" : $", ordered by {sort.Field} {sort.Direction.ToString().ToLowerInvariant()}"; return $"{result.RowCount} {noun} record{(result.RowCount == 1 ? "" : "s")} were returned{ordering}."; }
}
public static class ExternalPlanSemantics
{
    public static void ApplyTrustedMetadata(string question, BusinessQueryPlan plan, BusinessSchema schema)
    {
        NormalizeBaseEntityPaths(plan);
        ApplyRequestedRelationshipFields(question, plan, schema);
        if (!System.Text.RegularExpressions.Regex.IsMatch(question, @"\b(latest|newest|most recent|oldest)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return;
        var entity = schema.Entities.SingleOrDefault(x => x.Name.Equals(plan.Entity, StringComparison.OrdinalIgnoreCase));
        if (entity is null || string.IsNullOrWhiteSpace(entity.DefaultRecencyField))
            throw new ExternalDataException("RecencyNotConfigured", $"A default recency field is not configured for '{entity?.DisplayName ?? plan.Entity}'. An administrator must approve a date/time field and configure it for latest/oldest queries.");
        var direction = System.Text.RegularExpressions.Regex.IsMatch(question, @"\boldest\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            ? BusinessQuerySortDirection.Ascending
            : BusinessQuerySortDirection.Descending;
        plan.Sort = [new BusinessQuerySort(entity.DefaultRecencyField, direction)];
    }

    static void NormalizeBaseEntityPaths(BusinessQueryPlan plan)
    {
        string Field(string value)
        {
            var prefix = plan.Entity + ".";
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;
        }
        plan.Select = plan.Select.Select(Field).ToList();
        plan.Filters = plan.Filters.Select(x => x with { Field = Field(x.Field) }).ToList();
        plan.Sort = plan.Sort.Select(x => x with { Field = Field(x.Field) }).ToList();
        plan.GroupBy = plan.GroupBy.Select(Field).ToList();
        plan.Aggregations = plan.Aggregations.Select(x => x with { Field = x.Field is null ? null : Field(x.Field) }).ToList();
    }

    static void ApplyRequestedRelationshipFields(string question, BusinessQueryPlan plan, BusinessSchema schema)
    {
        var normalized = Normalize(question);
        var relationships = schema.Relationships
            .Where(x => x.FromEntity.Equals(plan.Entity, StringComparison.OrdinalIgnoreCase) && Matches(x, normalized))
            .ToList();
        if (relationships.Count == 0) return;

        foreach (var relationship in relationships)
        {
            var target = schema.Entities.SingleOrDefault(x => x.Name.Equals(relationship.ToEntity, StringComparison.OrdinalIgnoreCase));
            if (target is null) continue;
            var requested = target.Columns.Where(x => RequestedAttribute(x, normalized)).ToList();
            if (requested.Count == 0)
            {
                if (ContainsWord(normalized, "name") || ContainsWord(normalized, "email"))
                    throw new ExternalDataException("RelatedFieldNotConfigured", $"The {relationship.Name ?? relationship.ToEntity} relationship is available, but no approved requested display field is configured.");
                continue;
            }

            var relationshipName = relationship.Name ?? relationship.ToEntity;
            plan.Select.RemoveAll(x => x.Equals(relationship.FromField, StringComparison.OrdinalIgnoreCase));
            plan.Select.RemoveAll(x =>
            {
                var prefix = x.Split('.', 2)[0];
                return prefix.Equals(relationship.ToEntity, StringComparison.OrdinalIgnoreCase)
                    && !prefix.Equals(relationshipName, StringComparison.OrdinalIgnoreCase);
            });
            foreach (var field in requested.Select(x => $"{relationshipName}.{x.Name}"))
                if (!plan.Select.Contains(field, StringComparer.OrdinalIgnoreCase)) plan.Select.Add(field);
        }
    }

    static bool Matches(BusinessRelationshipSchema relationship, string question)
        => Terms(relationship.Name, relationship.Description, relationship.FromField)
            .Concat(relationship.Concepts ?? [])
            .Any(x => PhrasePresent(question, x));

    static bool RequestedAttribute(BusinessColumnSchema column, string question)
    {
        var terms = Terms(column.Name, column.DisplayName, column.Description).Concat(column.Concepts ?? []).ToList();
        if (terms.Any(x => PhrasePresent(question, x))) return true;
        return ContainsWord(question, "name") && terms.Any(x => Normalize(x).Split(' ').Contains("name"));
    }

    static IEnumerable<string> Terms(params string?[] values) => values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!);
    static bool PhrasePresent(string question, string value)
    {
        var phrase = Normalize(value);
        if (phrase.Length < 2 || phrase is "id") return false;
        return question.Contains(phrase, StringComparison.Ordinal) || phrase.Split(' ').Any(x => x.Length > 3 && x is not ("this" or "that" or "with" or "from") && ContainsWord(question, x));
    }
    static bool ContainsWord(string text, string word) => System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    static string Normalize(string value) => System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant().Replace("_", " "), @"[^a-z0-9]+", " ").Trim();
}
file sealed class InlineSchema(BusinessSchema schema) : IBusinessSchemaService { public BusinessSchema GetSchema() => schema; }

public sealed class ExternalBusinessQueryService(SupportAgentDbContext db, IExternalBusinessSchemaProvider schemas, IExternalBusinessQueryCompiler compiler, IExternalDataSourceSecretProvider secrets, IEnumerable<IExternalDataSourceConnector> connectors, IEnumerable<IAIProvider> providers, IOptions<AIOptions> options, ICurrentUserContext user, IAIUsageService usage) : IExternalBusinessQueryService
{
    public async Task<ExternalBusinessQueryResponse> AskAsync(Guid sourceId, string question, Guid? scopeId = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(question)) throw new ExternalDataException("QuestionRequired", "A business question is required."); var source = await db.ExternalDataSources.Include(x => x.Entities).ThenInclude(x => x.Fields).SingleOrDefaultAsync(x => x.Id == sourceId && x.IsEnabled, token) ?? throw new ExternalDataException("SourceUnavailable", "The external data source is unavailable."); var schema = await schemas.GetSchemaAsync(sourceId, scopeId, token); if (schema.Entities.Count == 0) throw new ExternalDataException("SchemaNotConfigured", "No approved schema mappings are configured for this source and scope."); var provider = providers.First(x => x.ProviderName.Equals(options.Value.Provider, StringComparison.OrdinalIgnoreCase)); var selected = new BusinessSchemaSelector(new InlineSchema(schema)).Select(question); var schemaJson = JsonSerializer.Serialize(selected, BusinessQueryPlanner.JsonOptions); var systemPrompt = $"Create a read-only BusinessQueryPlan as JSON only. Never output SQL. Use only this approved logical schema. Traverse related data only with RelationshipName.Field paths, at most 3 relationships. Resolve requested human-readable related attributes (for example a person's name) through the matching approved relationship; never substitute a foreign-key identifier for a requested name. Choose relationships from their Name, Description, Concepts, and source-field meaning. Add filters only for conditions explicitly stated by the user; never invent status, date, identifier, or other values. For latest/newest/oldest, use the base entity's DefaultRecencyField; never guess another date or status field. Latest/newest/oldest means sort and limit, not a filter. For row-list requests such as show/list/latest, aggregations must be empty. Use aggregations only when the user explicitly asks for a count, total, average, minimum, or maximum. Never mix selected row fields with aggregations unless every selected field is grouped. If a relationship is ambiguous, request clarification. Maximum limit 100. Schema: {schemaJson}"; var ai = await provider.GenerateAsync(new AIRequest { SystemPrompt = systemPrompt, Prompt = question, ResponseFormatJsonSchema = BusinessQueryPlanner.PlanSchema }, token); await usage.RecordUsageAsync(ai, "ExternalBusinessQueryPlanner", token); BusinessQueryPlan plan; try { plan = JsonSerializer.Deserialize<BusinessQueryPlan>(ai.Text ?? "", BusinessQueryPlanner.JsonOptions) ?? throw new JsonException(); } catch (JsonException) { throw new ExternalDataException("PlannerInvalid", "The external business question could not be mapped safely."); }
        if (plan.RequiresClarification) throw new ExternalDataException("ClarificationRequired", string.IsNullOrWhiteSpace(plan.ClarificationQuestion) ? "Please clarify which approved relationship should be used." : plan.ClarificationQuestion); ExternalPlanSemantics.ApplyTrustedMetadata(question, plan, selected); var validator = new BusinessQueryValidator(new InlineSchema(schema)); var validation = validator.Validate(plan); if (!validation.IsValid) { var repair = await provider.GenerateAsync(new AIRequest { SystemPrompt = systemPrompt, Prompt = $"{question}\nThe previous plan was rejected: {string.Join(" ", validation.Errors)} Return one corrected plan and do not repeat that invalid combination.", ResponseFormatJsonSchema = BusinessQueryPlanner.PlanSchema }, token); await usage.RecordUsageAsync(repair, "ExternalBusinessQueryPlannerRepair", token); ai = repair; try { plan = JsonSerializer.Deserialize<BusinessQueryPlan>(ai.Text ?? "", BusinessQueryPlanner.JsonOptions) ?? throw new JsonException(); } catch (JsonException) { throw new ExternalDataException("PlannerInvalid", "The external business question could not be mapped safely."); } ExternalPlanSemantics.ApplyTrustedMetadata(question, plan, selected); validation = validator.Validate(plan); }
        if (!validation.IsValid) { await Audit(false, "InvalidPlan", plan, 0, 0); throw new ExternalDataException("InvalidPlan", string.Join(" ", validation.Errors)); }
        var allowed = schema.Entities.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase); var entities = source.Entities.Where(x => allowed.Contains(x.BusinessEntityName) && x.IsValid).ToList(); var relationships = await db.ExternalRelationshipMappings.Where(x => x.ExternalDataSourceId == sourceId && x.IsEnabled && allowed.Contains(x.FromEntity) && allowed.Contains(x.ToEntity)).ToListAsync(token); var compiled = compiler.Compile(plan, schema, entities, relationships); BusinessQueryResult result; try { result = await connectors.Single(x => x.Provider == source.Provider).ExecuteAsync(secrets.GetConnectionString(source.SecretKey), compiled, plan.Limit, token); } catch (Exception ex) { await Audit(false, ex is ExternalDataException e ? e.Code : "ExternalQueryFailed", plan, 0, 0); throw; }
        var explanation = result.RowCount == 0 ? new AIResponse { Text = "No matching records were found.", Provider = ai.Provider, Model = ai.Model } : await provider.GenerateAsync(new AIRequest { SystemPrompt = "Explain the trusted result rows in natural language only. Never output SQL, pseudo-SQL, code, commands, or fields absent from the rows. Do not claim a name was resolved when only an identifier was returned.", Prompt = $"Original question: {question}\nDisplay columns: {JsonSerializer.Serialize(result.Columns)}\nTrusted rows: {JsonSerializer.Serialize(result.Rows)}\nRow count: {result.RowCount}" }, token); if (result.RowCount > 0) await usage.RecordUsageAsync(explanation, "ExternalBusinessQueryExplanation", token); await Audit(true, null, plan, result.RowCount, result.DurationMs); var entity = schema.Entities.Single(x => x.Name.Equals(plan.Entity, StringComparison.OrdinalIgnoreCase)); var display = result.Columns.Select(c => { var f = entity.Columns.FirstOrDefault(x => x.Name.Equals(c, StringComparison.OrdinalIgnoreCase)); return new BusinessQueryColumnDisplay(c, f?.DisplayName ?? c, f?.DataType ?? "String"); }).ToList(); string DescribePath(string field) { var parts = field.Split('.'); var current = plan.Entity; var path = new List<string> { current }; foreach (var segment in parts[..^1]) { var relation = relationships.SingleOrDefault(x => x.FromEntity.Equals(current, StringComparison.OrdinalIgnoreCase) && x.Name.Equals(segment, StringComparison.OrdinalIgnoreCase)); if (relation is null) break; path.Add(relation.Name); path.Add(relation.ToEntity); current = relation.ToEntity; } return string.Join(" → ", path); }
        var logicalJoins = plan.Select.Concat(plan.Filters.Select(x => x.Field)).Concat(plan.GroupBy).Concat(plan.Sort.Select(x => x.Field)).Concat(plan.Aggregations.Where(x => x.Field is not null).Select(x => x.Field!)).Where(x => x.Contains('.')).Select(DescribePath).Distinct().ToList(); var safeAnswer = ExternalExplanationSafety.SafeAnswer(explanation.Text, plan, result, entity.DisplayName); var scopeName = scopeId.HasValue ? await db.ExternalDataScopes.Where(x => x.Id == scopeId.Value && x.ExternalDataSourceId == sourceId).Select(x => x.Name).SingleOrDefaultAsync(token) : null; var response = new BusinessQueryResponse(safeAnswer, plan, result, display, explanation.Provider, explanation.Model, ai.InputTokens + explanation.InputTokens, ai.OutputTokens + explanation.OutputTokens, logicalJoins, source.Name, scopeName, selected.Entities.Select(x => x.Name).ToList()); return new(source.Id, source.Name, response);
        async Task Audit(bool success, string? code, BusinessQueryPlan p, int rows, long duration) { db.BusinessQueryAudits.Add(new BusinessQueryAudit { Id = Guid.NewGuid(), OrganizationId = user.OrganizationId, ApplicationUserId = user.UserId, Question = question, QueryPlanJson = JsonSerializer.Serialize(p, BusinessQueryPlanner.JsonOptions), RowCount = rows, DurationMs = duration, Succeeded = success, ErrorCode = code, CreatedAt = DateTime.UtcNow, DataSourceId = source.Id, DataSourceName = source.Name, DataSourceProvider = source.Provider.ToString() }); await db.SaveChangesAsync(token); }
    }
}

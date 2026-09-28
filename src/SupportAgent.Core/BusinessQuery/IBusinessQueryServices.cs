namespace SupportAgent.Core.BusinessQuery;

public interface IBusinessSchemaService { BusinessSchema GetSchema(); }
public interface IBusinessSchemaSelector { BusinessSchema Select(string question); }
public interface IBusinessQueryPlanner { Task<BusinessQueryPlanningResult> CreatePlanAsync(string question, CancellationToken cancellationToken = default); }
public interface IBusinessQueryValidator { BusinessQueryValidationResult Validate(BusinessQueryPlan plan); }
public interface IBusinessQueryExecutor { Task<BusinessQueryResult> ExecuteAsync(BusinessQueryPlan plan, CancellationToken cancellationToken = default); }
public interface IBusinessQueryService { Task<BusinessQueryResponse> AskAsync(string question, CancellationToken cancellationToken = default); }

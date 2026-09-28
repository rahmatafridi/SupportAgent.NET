using SupportAgent.Core.BusinessQuery;
namespace SupportAgent.Core.ExternalData;
public interface IExternalDataSourceSecretProvider { string GetConnectionString(string secretKey); }
public interface IExternalDataSourceConnector { ExternalDataSourceProvider Provider{get;} Task<ExternalConnectionTestResult> TestAsync(string connectionString,CancellationToken token=default); Task<IReadOnlyList<ExternalDiscoveredObject>> DiscoverAsync(string connectionString,CancellationToken token=default); Task<BusinessQueryResult> ExecuteAsync(string connectionString,ExternalCompiledQuery query,int limit,CancellationToken token=default); }
public interface IExternalSchemaDiscoveryService { Task<IReadOnlyList<ExternalDiscoveredObject>> DiscoverAsync(Guid sourceId,CancellationToken token=default); }
public interface IExternalBusinessSchemaProvider { Task<BusinessSchema> GetSchemaAsync(Guid sourceId,Guid? scopeId=null,CancellationToken token=default); }
public interface IExternalBusinessQueryCompiler { ExternalCompiledQuery Compile(BusinessQueryPlan plan,BusinessSchema schema,IReadOnlyList<ExternalEntityMapping> entities,IReadOnlyList<ExternalRelationshipMapping> relationships); }
public interface IExternalBusinessQueryService { Task<ExternalBusinessQueryResponse> AskAsync(Guid sourceId,string question,Guid? scopeId=null,CancellationToken token=default); }

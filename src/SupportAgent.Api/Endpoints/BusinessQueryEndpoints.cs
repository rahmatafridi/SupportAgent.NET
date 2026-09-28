using Microsoft.AspNetCore.Antiforgery;
using SupportAgent.Api.Authorization;
using SupportAgent.Api.Security;
using SupportAgent.Core.BusinessQuery;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.BusinessQuery;

namespace SupportAgent.Api.Endpoints;

public record BusinessQueryRequest(string Question, Guid? DataSourceId = null, Guid? ScopeId = null);

public static class BusinessQueryEndpoints
{
    public static IEndpointRouteBuilder MapBusinessQueryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/business-query", async (BusinessQueryRequest request, IBusinessQueryService service, SupportAgent.Core.ExternalData.IExternalBusinessQueryService external,
            ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                var response = request.DataSourceId.HasValue ? (await external.AskAsync(request.DataSourceId.Value,request.Question,request.ScopeId,cancellationToken)).Response : await service.AskAsync(request.Question, cancellationToken);
                return Results.Ok(response);
            }
            catch (BusinessQueryException exception)
            {
                return Results.BadRequest(new { error = exception.Message, code = exception.Code });
            }
            catch (SupportAgent.Infrastructure.ExternalData.ExternalDataException exception)
            {
                return Results.BadRequest(new { error = exception.Message, code = exception.Code });
            }
            catch (Exception exception)
            {
                loggerFactory.CreateLogger("BusinessQueryEndpoint").LogError(exception, "Business query endpoint failed safely.");
                return Results.Problem("The business query could not be completed safely.", statusCode: 500);
            }
        }).RequireAuthorization(AuthorizationPolicies.AiAccess)
          .RequireRateLimiting("ai")
          .AddEndpointFilter<AntiforgeryEndpointFilter>();
        return app;
    }
}

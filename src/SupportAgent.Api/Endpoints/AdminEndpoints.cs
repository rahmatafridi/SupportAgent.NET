using SupportAgent.Core.Interfaces;
using SupportAgent.Api.Authorization;

namespace SupportAgent.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/ai-usage", async (IAIUsageService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCurrentMonthUsageAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        return app;
    }
}

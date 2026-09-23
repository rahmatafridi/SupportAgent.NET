using SupportAgent.Core.DTOs;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Health check endpoints used to verify that the API is running.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps health check routes to the application pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/health
        // Returns a simple status payload so clients can verify the backend is online.
        app.MapGet("/api/health", () =>
            Results.Ok(new HealthResponse("ok", "SupportAgent.NET")))
            .WithName("GetHealth")
            .WithSummary("Returns API health status.")
            .WithDescription("Used by the React homepage and monitoring tools to confirm the backend is running.");

        return app;
    }
}

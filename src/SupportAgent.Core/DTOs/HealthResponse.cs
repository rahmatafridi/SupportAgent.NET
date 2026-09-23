namespace SupportAgent.Core.DTOs;

/// <summary>
/// Simple health check payload returned by <c>GET /api/health</c>.
/// </summary>
/// <param name="Status">Health status text, usually "ok".</param>
/// <param name="Application">Application name shown to clients and monitoring tools.</param>
public record HealthResponse(string Status, string Application);

namespace SupportAgent.Core.Interfaces;

public interface ICopilotActionService
{
    Task<CopilotActionResult> ExecuteAsync(
        Guid actionId,
        int ticketId,
        Guid conversationId,
        bool confirmed,
        CancellationToken cancellationToken = default);
}

public record CopilotActionResult(
    string Action,
    string ActionLabel,
    bool Success,
    string ResultJson,
    CopilotAskResult UpdatedResponse);

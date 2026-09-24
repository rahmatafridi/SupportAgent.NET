using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

public interface IAIUsageService
{
    Task RecordUsageAsync(AIResponse response, string requestType, CancellationToken cancellationToken = default);
    Task<AIUsageSummary> GetCurrentMonthUsageAsync(CancellationToken cancellationToken = default);
}

public sealed class AIUsageSummary
{
    public int TotalRequests { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public IReadOnlyList<AIUsageBreakdown> Breakdown { get; set; } = [];
}

public sealed class AIUsageBreakdown
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Requests { get; set; }
    public long TotalTokens { get; set; }
}

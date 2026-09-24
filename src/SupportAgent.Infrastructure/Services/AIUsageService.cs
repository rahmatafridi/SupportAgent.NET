using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

public sealed class AIUsageService(
    SupportAgentDbContext dbContext,
    ICurrentUserContext currentUser) : IAIUsageService
{
    public async Task RecordUsageAsync(
        AIResponse response,
        string requestType,
        CancellationToken cancellationToken = default)
    {
        dbContext.AIUsageRecords.Add(new AIUsageRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = currentUser.OrganizationId,
            UserId = currentUser.UserId,
            Provider = response.Provider,
            Model = response.Model,
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            TotalTokens = response.TotalTokens,
            DurationMs = (long)response.Duration.TotalMilliseconds,
            RequestType = requestType,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AIUsageSummary> GetCurrentMonthUsageAsync(CancellationToken cancellationToken = default)
    {
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var records = dbContext.AIUsageRecords.AsNoTracking().Where(x => x.CreatedAt >= start);
        var totals = await records.GroupBy(_ => 1).Select(group => new AIUsageSummary
        {
            TotalRequests = group.Count(),
            InputTokens = group.Sum(x => (long)x.InputTokens),
            OutputTokens = group.Sum(x => (long)x.OutputTokens),
            TotalTokens = group.Sum(x => (long)x.TotalTokens)
        }).FirstOrDefaultAsync(cancellationToken) ?? new AIUsageSummary();

        totals.Breakdown = await records.GroupBy(x => new { x.Provider, x.Model })
            .Select(group => new AIUsageBreakdown
            {
                Provider = group.Key.Provider,
                Model = group.Key.Model,
                Requests = group.Count(),
                TotalTokens = group.Sum(x => (long)x.TotalTokens)
            }).ToListAsync(cancellationToken);
        return totals;
    }
}

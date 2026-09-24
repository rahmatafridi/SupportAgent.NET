namespace SupportAgent.Core.Models;

/// <summary>Non-content billing and operational metadata for one successful AI request.</summary>
public class AIUsageRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public long DurationMs { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

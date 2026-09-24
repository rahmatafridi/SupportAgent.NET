namespace SupportAgent.Infrastructure.Identity;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public bool AllowSelfRegistration { get; set; }
}

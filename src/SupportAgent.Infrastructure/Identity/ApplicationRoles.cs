namespace SupportAgent.Infrastructure.Identity;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string SupportAgent = "SupportAgent";
    public const string Viewer = "Viewer";
    public const string Customer = "Customer";
    public const string AiUsers = Admin + "," + SupportAgent;
}

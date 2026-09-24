using System.Reflection;
using SupportAgent.Api.Configuration;
using SupportAgent.Api.Endpoints;
using SupportAgent.Infrastructure;
using SupportAgent.Infrastructure.AI;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using SupportAgent.Api.Authentication;
using SupportAgent.Api.Authorization;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;

// Application entry point.
// Registers services, maps API endpoints, and starts the web server.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "SupportAgent.NET",
        Version = "v1",
        Description = "Open-source AI customer support agent starter kit API."
    });

    var xmlFiles = new[]
    {
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml",
        $"{typeof(SupportAgent.Core.DTOs.HealthResponse).Assembly.GetName().Name}.xml",
        $"{typeof(SupportAgent.Infrastructure.DependencyInjection).Assembly.GetName().Name}.xml"
    };

    foreach (var xmlFile in xmlFiles)
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }
});
builder.Services.AddDevelopmentCors();
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection(AuthenticationOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAI(builder.Configuration);
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<SupportAgentDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationClaimsPrincipalFactory>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SupportAgent.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AiAccess, policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.SupportAgent))
    .AddPolicy(AuthorizationPolicies.KnowledgeSearch, policy => policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.SupportAgent))
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(ApplicationRoles.Admin));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
var aiRequestsPerMinute = builder.Configuration.GetValue("RateLimiting:AIRequestsPerMinute", 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = aiRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Identity and tenant seeders require the Phase 8 tables to exist first.
    await app.Services.MigrateDatabaseAsync();
    await app.Services.SeedIdentityRolesAsync();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SupportAgent.NET v1");
        options.RoutePrefix = "swagger";
    });
    app.MapOpenApi();
    app.UseCors(CorsConfiguration.DevelopmentPolicyName);
    await app.Services.SeedDevelopmentDataAsync();
}
else if (app.Environment.IsEnvironment("Testing"))
{
    // Integration tests replace SQL Server with an in-memory provider.
    await app.Services.SeedIdentityRolesAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

// Map all HTTP endpoints.
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapCustomerEndpoints();
app.MapOrderEndpoints();
app.MapTicketEndpoints();
app.MapKnowledgeEndpoints();
app.MapAIEndpoints();
app.MapCopilotEndpoints();
app.MapAdminEndpoints();

app.Run();

public partial class Program;

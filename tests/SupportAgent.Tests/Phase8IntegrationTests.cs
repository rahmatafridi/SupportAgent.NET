using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using SupportAgent.Core.Models;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Tests;

public sealed class Phase8ApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"phase8-{Guid.NewGuid()}";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused");
        builder.UseSetting("Authentication:AllowSelfRegistration", "true");
        builder.UseSetting("RateLimiting:AIRequestsPerMinute", "3");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:AllowSelfRegistration"] = "true",
            ["RateLimiting:AIRequestsPerMinute"] = "3",
            ["ConnectionStrings:DefaultConnection"] = "Server=unused"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportAgentDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportAgentDbContext>>();
            services.AddDbContext<SupportAgentDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<IEmbeddingService>();
            services.AddScoped<IEmbeddingService, FakeEmbeddingService>();
        });
    }
}

public class Phase8IntegrationTests : IClassFixture<Phase8ApplicationFactory>
{
    private readonly Phase8ApplicationFactory _factory;
    public Phase8IntegrationTests(Phase8ApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Authentication_cookie_csrf_and_logout_flow_works()
    {
        using var client = CreateClient();
        var token = await GetCsrfAsync(client);
        var register = await PostAsync(client, "/api/auth/register", new
        {
            organizationName = "Acme Support", email = $"admin-{Guid.NewGuid():N}@example.com", password = "ValidPassword123!"
        }, token);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var email = (await register.Content.ReadFromJsonAsync<RegisterResult>())!.Email;
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await manager.FindByEmailAsync(email);
            Assert.NotNull(stored);
            Assert.True(await manager.CheckPasswordAsync(stored, "ValidPassword123!"));
        }

        var loginWithoutCsrf = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ValidPassword123!" });
        Assert.Equal(HttpStatusCode.BadRequest, loginWithoutCsrf.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(client, "/api/auth/login", new { email, password = "ValidPassword123!" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        token = await GetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(client, "/api/auth/logout", new { }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Authentication_and_roles_are_enforced()
    {
        using var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/tickets")).StatusCode);

        var (viewer, _, token, viewerEmail) = await CreateAuthenticatedUserAsync("Viewer Org");
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await manager.FindByEmailAsync(viewerEmail))!;
            await manager.RemoveFromRoleAsync(user, ApplicationRoles.Admin);
            await manager.AddToRoleAsync(user, ApplicationRoles.Viewer);
        }
        await PostAsync(viewer, "/api/auth/logout", new { }, token);
        token = await GetCsrfAsync(viewer);
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(viewer, "/api/auth/login", new { email = viewerEmail, password = "ValidPassword123!" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/admin/ai-usage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(viewer, "/api/ai/chat", new { message = "hello" }, token)).StatusCode);
    }

    [Fact]
    public async Task Tenant_filter_returns_not_found_for_another_organization_customer()
    {
        var (tenantA, organizationA, _, _) = await CreateAuthenticatedUserAsync("Tenant A");
        var (tenantB, _, _, _) = await CreateAuthenticatedUserAsync("Tenant B");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportAgentDbContext>();
            db.Customers.Add(new Customer { Id = 9876, OrganizationId = organizationA, FirstName = "Tenant", LastName = "A", Email = "tenant-a-customer@example.com", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await tenantA.GetAsync("/api/customers/9876")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.GetAsync("/api/customers/9876")).StatusCode);
    }

    [Fact]
    public async Task AI_endpoint_is_rate_limited_per_user()
    {
        var (client, _, token, _) = await CreateAuthenticatedUserAsync("Rate Org");
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(client, "/api/ai/chat", new { message = "" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync(client, "/api/ai/chat", new { message = "" }, token)).StatusCode);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    private async Task<(HttpClient Client, Guid OrganizationId, string Token, string Email)> CreateAuthenticatedUserAsync(string organization)
    {
        var client = CreateClient(); var token = await GetCsrfAsync(client); var email = $"user-{Guid.NewGuid():N}@example.com";
        var registration = await PostAsync(client, "/api/auth/register", new { organizationName = organization, email, password = "ValidPassword123!" }, token);
        registration.EnsureSuccessStatusCode();
        var result = (await registration.Content.ReadFromJsonAsync<RegisterResult>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(client, "/api/auth/login", new { email, password = "ValidPassword123!" }, token)).StatusCode);
        token = await GetCsrfAsync(client);
        return (client, result.Id, token, email);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfResult>())!.Token;

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    private sealed record CsrfResult(string Token);
    private sealed record RegisterResult(Guid Id, string Name, string Email);
}

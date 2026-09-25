using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Tests;

public class Phase11IntegrationTests(Phase8ApplicationFactory factory) : IClassFixture<Phase8ApplicationFactory>
{
    [Fact]
    public async Task Customer_login_portal_and_security_boundaries_work()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); var token = await Csrf(client); var email = $"customer-{Guid.NewGuid():N}@example.com";
        var register = await Send(client, HttpMethod.Post, "/api/auth/register", new { organizationName = "Customer Org", email, password = "ValidPassword123!" }, token); register.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await manager.FindByEmailAsync(email))!;
            await manager.RemoveFromRoleAsync(user, ApplicationRoles.Admin); await manager.AddToRoleAsync(user, ApplicationRoles.Customer);
            var db = scope.ServiceProvider.GetRequiredService<SupportAgentDbContext>(); db.Customers.Add(new Customer { OrganizationId = user.OrganizationId, ApplicationUserId = user.Id, FirstName = "Portal", LastName = "Customer", Email = email, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await Send(client, HttpMethod.Post, "/api/auth/login", new { email, password = "ValidPassword123!" }, token)).StatusCode); token = await Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/portal/me")).StatusCode);
        var create = await Send(client, HttpMethod.Post, "/api/portal/tickets", new { subject = "Need help", priority = "Medium", message = "Initial message" }, token); Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var ticket = (await create.Content.ReadFromJsonAsync<PortalTicket>())!;
        Assert.Equal(HttpStatusCode.Created, (await Send(client, HttpMethod.Post, $"/api/portal/tickets/{ticket.Id}/messages", new { message = "Follow up" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, HttpMethod.Post, "/api/copilot/ask", new { ticketId = ticket.Id, message = "AI" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/knowledge/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/ai-usage")).StatusCode);
    }
    private static async Task<string> Csrf(HttpClient client) => (await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfResponse>())!.Token;
    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, object body, string token) { var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) }; request.Headers.Add("X-CSRF-TOKEN", token); return client.SendAsync(request); }
    private sealed record CsrfResponse(string Token);
}

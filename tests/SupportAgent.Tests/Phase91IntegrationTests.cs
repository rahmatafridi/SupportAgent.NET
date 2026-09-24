using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Tests;

public class Phase91IntegrationTests : IClassFixture<Phase8ApplicationFactory>
{
    private readonly Phase8ApplicationFactory _factory;
    public Phase91IntegrationTests(Phase8ApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(ApplicationRoles.Admin)]
    [InlineData(ApplicationRoles.SupportAgent)]
    public async Task Support_roles_can_create_update_and_reply(string role)
    {
        var session = await LoginAsync(role); var customerId = Random.Shared.Next(20000, 90000);
        await AddCustomerAsync(customerId, session.OrganizationId);
        var create = await SendAsync(session.Client, HttpMethod.Post, "/api/tickets", new { customerId, subject = "Order not received", priority = "High", message = "My package has not arrived." }, session.Token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var ticket = (await create.Content.ReadFromJsonAsync<TicketResult>())!;
        Assert.Equal(session.OrganizationId, await TicketOrganizationAsync(ticket.Id));
        Assert.Equal("Customer", (await session.Client.GetFromJsonAsync<List<MessageResult>>($"/api/tickets/{ticket.Id}/messages"))!.Single().SenderType);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(session.Client, HttpMethod.Patch, $"/api/tickets/{ticket.Id}/status", new { status = "Closed" }, session.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(session.Client, HttpMethod.Patch, $"/api/tickets/{ticket.Id}/priority", new { priority = "Low" }, session.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(session.Client, HttpMethod.Post, $"/api/tickets/{ticket.Id}/messages", new { message = "We are checking with the carrier." }, session.Token)).StatusCode);
        Assert.Contains((await session.Client.GetFromJsonAsync<List<MessageResult>>($"/api/tickets/{ticket.Id}/messages"))!, message => message.SenderType == "Agent");
    }

    [Fact]
    public async Task Viewer_cannot_create_or_reply()
    {
        var viewer = await LoginAsync(ApplicationRoles.Viewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(viewer.Client, HttpMethod.Post, "/api/tickets", new { customerId = 1, subject = "Subject", priority = "Medium", message = "Message" }, viewer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(viewer.Client, HttpMethod.Post, "/api/tickets/1/messages", new { message = "Reply" }, viewer.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(viewer.Client, HttpMethod.Post, "/api/tickets/1/notes", new { content = "Private note" }, viewer.Token)).StatusCode);
    }

    [Fact]
    public async Task Ticket_list_does_not_require_assigned_to_me_query_parameter()
    {
        var session = await LoginAsync(ApplicationRoles.Admin);
        var response = await session.Client.GetAsync("/api/tickets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_mutations_return_not_found()
    {
        var tenantA = await LoginAsync(ApplicationRoles.Admin); var tenantB = await LoginAsync(ApplicationRoles.Admin); var customerId = Random.Shared.Next(90001, 120000);
        await AddCustomerAsync(customerId, tenantA.OrganizationId);
        var create = await SendAsync(tenantA.Client, HttpMethod.Post, "/api/tickets", new { customerId, subject = "Tenant A", priority = "Medium", message = "Private" }, tenantA.Token);
        var ticket = (await create.Content.ReadFromJsonAsync<TicketResult>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(tenantB.Client, HttpMethod.Patch, $"/api/tickets/{ticket.Id}/status", new { status = "Closed" }, tenantB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(tenantB.Client, HttpMethod.Post, $"/api/tickets/{ticket.Id}/messages", new { message = "Cross tenant" }, tenantB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(tenantB.Client, HttpMethod.Post, $"/api/tickets/{ticket.Id}/notes", new { content = "Cross tenant" }, tenantB.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(tenantB.Client, HttpMethod.Post, "/api/tickets", new { customerId, subject = "Invalid", priority = "Low", message = "Invalid" }, tenantB.Token)).StatusCode);
    }

    private async Task<Session> LoginAsync(string role)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); var token = await CsrfAsync(client); var email = $"phase91-{Guid.NewGuid():N}@example.com";
        var registration = await SendAsync(client, HttpMethod.Post, "/api/auth/register", new { organizationName = $"Org {Guid.NewGuid():N}", email, password = "ValidPassword123!" }, token); registration.EnsureSuccessStatusCode();
        var organizationId = (await registration.Content.ReadFromJsonAsync<RegisterResult>())!.Id;
        if (role != ApplicationRoles.Admin) using (var scope = _factory.Services.CreateScope()) { var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await manager.FindByEmailAsync(email))!; await manager.RemoveFromRoleAsync(user, ApplicationRoles.Admin); await manager.AddToRoleAsync(user, role); }
        await SendAsync(client, HttpMethod.Post, "/api/auth/login", new { email, password = "ValidPassword123!" }, token);
        return new Session(client, await CsrfAsync(client), organizationId);
    }
    private async Task AddCustomerAsync(int id, Guid organizationId) { using var scope = _factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SupportAgentDbContext>(); db.Customers.Add(new Customer { Id = id, OrganizationId = organizationId, FirstName = "New", LastName = "Customer", Email = $"{id}@example.com", CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync(); }
    private async Task<Guid> TicketOrganizationAsync(int id) { using var scope = _factory.Services.CreateScope(); return (await scope.ServiceProvider.GetRequiredService<SupportAgentDbContext>().Tickets.IgnoreQueryFilters().SingleAsync(ticket => ticket.Id == id)).OrganizationId; }
    private static async Task<string> CsrfAsync(HttpClient client) => (await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<Csrf>())!.Token;
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object body, string token) { var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) }; request.Headers.Add("X-CSRF-TOKEN", token); return client.SendAsync(request); }
    private sealed record Session(HttpClient Client, string Token, Guid OrganizationId); private sealed record Csrf(string Token); private sealed record RegisterResult(Guid Id); private sealed record TicketResult(int Id); private sealed record MessageResult(string SenderType);
}

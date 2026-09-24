using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Tests;

public class Phase9IntegrationTests : IClassFixture<Phase8ApplicationFactory>
{
    private readonly Phase8ApplicationFactory _factory;
    public Phase9IntegrationTests(Phase8ApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_can_upload_list_and_delete_document()
    {
        var (client, token, _) = await LoginNewAdminAsync();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Uploaded escalation policy for priority support cases.")) { Headers = { ContentType = new("text/plain") } }, "file", "escalation-policy.txt");
        form.Add(new StringContent("Escalation Policy"), "title");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/knowledge/upload") { Content = form }; request.Headers.Add("X-CSRF-TOKEN", token);
        var upload = await client.SendAsync(request); Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploaded = (await upload.Content.ReadFromJsonAsync<UploadResult>())!; Assert.True(uploaded.ChunksCreated > 0);
        var documents = await client.GetFromJsonAsync<List<DocumentResult>>("/api/knowledge/documents");
        Assert.Contains(documents!, x => x.Id == uploaded.DocumentId && x.ChunkCount > 0);
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/knowledge/documents/{uploaded.DocumentId}"); delete.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
    }

    [Theory]
    [InlineData(ApplicationRoles.SupportAgent, HttpStatusCode.Forbidden)]
    [InlineData(ApplicationRoles.Viewer, HttpStatusCode.Forbidden)]
    public async Task Non_admin_cannot_upload(string role, HttpStatusCode expected)
    {
        var (client, _, email) = await LoginNewAdminAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await manager.FindByEmailAsync(email))!;
            await manager.RemoveFromRoleAsync(user, ApplicationRoles.Admin);
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(); if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
            await manager.AddToRoleAsync(user, role);
        }
        await LogoutAsync(client); var token = await CsrfAsync(client);
        await PostJsonAsync(client, "/api/auth/login", new { email, password = "ValidPassword123!" }, token);
        token = await CsrfAsync(client);
        using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent("text"u8.ToArray()) { Headers = { ContentType = new("text/plain") } }, "file", "test.txt");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/knowledge/upload") { Content = form }; request.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
        if (role == ApplicationRoles.Viewer) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/knowledge/documents")).StatusCode);
    }

    private async Task<(HttpClient Client, string Token, string Email)> LoginNewAdminAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); var token = await CsrfAsync(client); var email = $"phase9-{Guid.NewGuid():N}@example.com";
        (await PostJsonAsync(client, "/api/auth/register", new { organizationName = "Phase 9", email, password = "ValidPassword123!" }, token)).EnsureSuccessStatusCode();
        (await PostJsonAsync(client, "/api/auth/login", new { email, password = "ValidPassword123!" }, token)).EnsureSuccessStatusCode();
        return (client, await CsrfAsync(client), email);
    }
    private static async Task LogoutAsync(HttpClient client) { var token = await CsrfAsync(client); await PostJsonAsync(client, "/api/auth/logout", new { }, token); }
    private static async Task<string> CsrfAsync(HttpClient client) => (await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<Csrf>())!.Token;
    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object value, string token) { var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(value) }; request.Headers.Add("X-CSRF-TOKEN", token); return client.SendAsync(request); }
    private sealed record Csrf(string Token); private sealed record UploadResult(int DocumentId, int ChunksCreated); private sealed record DocumentResult(int Id, int ChunkCount);
}

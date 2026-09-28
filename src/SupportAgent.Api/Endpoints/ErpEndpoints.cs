using Microsoft.EntityFrameworkCore;
using SupportAgent.Api.Authorization;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Api.Endpoints;

public static class ErpEndpoints
{
    public static IEndpointRouteBuilder MapErpEndpoints(this IEndpointRouteBuilder app)
    {
        var staff = app.MapGroup("/api").RequireAuthorization(AuthorizationPolicies.InternalSupport);
        staff.MapGet("/products", async (string? search, IProductService s, CancellationToken ct) => Results.Ok(await s.SearchProductsAsync(search, ct)));
        staff.MapGet("/products/{id:int}", async (int id, IProductService s, CancellationToken ct) => await s.GetProductAsync(id, ct) is { } x ? Results.Ok(x) : Results.NotFound());
        staff.MapGet("/invoices", async (string? search, string? status, IInvoiceService s, CancellationToken ct) => Results.Ok(await s.SearchAsync(search, status, ct)));
        staff.MapGet("/invoices/{id:int}", async (int id, IInvoiceService s, CancellationToken ct) => await s.GetInvoiceAsync(id, ct) is { } x ? Results.Ok(x) : Results.NotFound());
        staff.MapGet("/invoices/number/{number}", async (string number, IInvoiceService s, CancellationToken ct) => await s.GetInvoiceByNumberAsync(number, ct) is { } x ? Results.Ok(x) : Results.NotFound());
        staff.MapGet("/customers/{id:int}/invoices", async (int id, IInvoiceService s, CancellationToken ct) => Results.Ok(await s.GetCustomerInvoicesAsync(id, ct)));
        staff.MapGet("/customers/{id:int}/balance", async (int id, IInvoiceService s, CancellationToken ct) => Results.Ok(await s.GetCustomerBalanceAsync(id, ct)));
        staff.MapGet("/invoices/{id:int}/payments", async (int id, IPaymentService s, CancellationToken ct) => Results.Ok(await s.GetInvoicePaymentsAsync(id, ct)));
        staff.MapGet("/inventory/low-stock", async (IInventoryService s, CancellationToken ct) => Results.Ok(await s.GetLowStockProductsAsync(ct)));
        staff.MapGet("/invoices/{id:int}/pdf", Download);

        var portal = app.MapGroup("/api/portal/invoices").RequireAuthorization(AuthorizationPolicies.CustomerPortal);
        portal.MapGet("/", async (SupportAgentDbContext db, ICurrentUserContext user, IInvoiceService s, CancellationToken ct) => { var cid = await db.Customers.Where(x => x.ApplicationUserId == user.UserId).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct); return cid is null ? Results.NotFound() : Results.Ok(await s.GetCustomerInvoicesAsync(cid.Value, ct)); });
        portal.MapGet("/{id:int}", async (int id, SupportAgentDbContext db, ICurrentUserContext user, IInvoiceService s, CancellationToken ct) => { var cid = await db.Customers.Where(x => x.ApplicationUserId == user.UserId).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct); var x = await s.GetInvoiceAsync(id, ct); return cid is not null && x?.CustomerId == cid ? Results.Ok(x) : Results.NotFound(); });
        portal.MapGet("/{id:int}/pdf", async (int id, SupportAgentDbContext db, ICurrentUserContext user, IInvoiceService invoices, IInvoiceDocumentService docs, CancellationToken ct) => { var cid = await db.Customers.Where(x => x.ApplicationUserId == user.UserId).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct); var invoice = await invoices.GetInvoiceAsync(id, ct); if (cid is null || invoice?.CustomerId != cid) return Results.NotFound(); var bytes = await docs.GenerateAsync(id, ct); return Results.File(bytes!, "application/pdf", $"{invoice.InvoiceNumber}.pdf"); });
        return app;
    }
    private static async Task<IResult> Download(int id, IInvoiceService invoices, IInvoiceDocumentService docs, CancellationToken ct) { var invoice = await invoices.GetInvoiceAsync(id, ct); if (invoice is null) return Results.NotFound(); return Results.File((await docs.GenerateAsync(id, ct))!, "application/pdf", $"{invoice.InvoiceNumber}.pdf"); }
}

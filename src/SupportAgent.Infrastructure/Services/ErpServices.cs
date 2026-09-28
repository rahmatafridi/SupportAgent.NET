using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Fonts;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

public class InvoiceCalculator : IInvoiceCalculator
{
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public decimal LineTotal(decimal quantity, decimal unitPrice, decimal discount, decimal tax) => Money(quantity * unitPrice - discount + tax);
    public decimal InvoiceTotal(IEnumerable<InvoiceItem> items) => Money(items.Sum(x => LineTotal(x.Quantity, x.UnitPrice, x.DiscountAmount, x.TaxAmount)));
    public decimal Outstanding(decimal total, IEnumerable<Payment> payments) => Math.Max(0, Money(total - payments.Sum(x => x.Amount)));
}

public class ProductService(SupportAgentDbContext db) : IProductService
{
    public Task<Product?> GetProductAsync(int id, CancellationToken token = default) => db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, token);
    public async Task<IReadOnlyList<Product>> SearchProductsAsync(string? search = null, CancellationToken token = default) { var q = db.Products.AsNoTracking(); if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.Sku.Contains(search) || x.Name.Contains(search)); return await q.OrderBy(x => x.Name).ToListAsync(token); }
}

public class InvoiceService(SupportAgentDbContext db, IInvoiceCalculator calculator) : IInvoiceService
{
    private IQueryable<Invoice> Details() => db.Invoices.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).Include(x => x.Payments);
    private InvoiceDetail Map(Invoice x) { var paid = Math.Round(x.Payments.Sum(p => p.Amount), 2); return new(x.Id, x.CustomerId, $"{x.Customer.FirstName} {x.Customer.LastName}".Trim(), x.Customer.Email, x.InvoiceNumber, x.Status, x.IssueDate, x.DueDate, x.Subtotal, x.TaxAmount, x.DiscountAmount, x.TotalAmount, paid, calculator.Outstanding(x.TotalAmount, x.Payments), x.Items.Select(i => new InvoiceLineDetail(i.Id, i.ProductId, i.Description, i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxAmount, i.LineTotal)).ToList(), x.Payments.Select(p => new PaymentDetail(p.Id, p.PaymentReference, p.Amount, p.PaymentDate, p.Method, p.Notes)).ToList()); }
    public async Task<InvoiceDetail?> GetInvoiceAsync(int id, CancellationToken token = default) { var x = await Details().FirstOrDefaultAsync(x => x.Id == id, token); return x is null ? null : Map(x); }
    public async Task<InvoiceDetail?> GetInvoiceByNumberAsync(string number, CancellationToken token = default) { var x = await Details().FirstOrDefaultAsync(x => x.InvoiceNumber == number, token); return x is null ? null : Map(x); }
    public async Task<IReadOnlyList<InvoiceListItem>> GetCustomerInvoicesAsync(int customerId, CancellationToken token = default) => (await Details().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.IssueDate).ToListAsync(token)).Select(List).ToList();
    public async Task<IReadOnlyList<InvoiceListItem>> SearchAsync(string? search, string? status, CancellationToken token = default) { var q = Details(); if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.InvoiceNumber.Contains(search) || (x.Customer.FirstName + " " + x.Customer.LastName).Contains(search)); if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status); return (await q.OrderByDescending(x => x.IssueDate).ToListAsync(token)).Select(List).ToList(); }
    public async Task<CustomerBalance> GetCustomerBalanceAsync(int customerId, CancellationToken token = default) { var xs = await Details().Where(x => x.CustomerId == customerId && x.Status != "Cancelled" && x.Status != "Draft").ToListAsync(token); var total = xs.Sum(x => x.TotalAmount); var paid = xs.SelectMany(x => x.Payments).Sum(x => x.Amount); return new(customerId, total, paid, Math.Max(0, total - paid)); }
    public async Task<IReadOnlyList<InvoiceListItem>> GetOutstandingInvoicesAsync(int customerId, CancellationToken token = default) => (await GetCustomerInvoicesAsync(customerId, token)).Where(x => x.OutstandingBalance > 0).ToList();
    public async Task<SalesSummary> GetSalesSummaryAsync(DateTime start, DateTime end, CancellationToken token = default) { if (end < start || (end - start).TotalDays > 366) throw new ArgumentException("Date range must be valid and no longer than 366 days."); var xs = await Details().Where(x => x.IssueDate >= start && x.IssueDate < end.AddDays(1) && x.Status != "Cancelled" && x.Status != "Draft").ToListAsync(token); var sales = xs.Sum(x => x.TotalAmount); var paid = xs.SelectMany(x => x.Payments).Sum(x => x.Amount); return new(start, end, xs.Count, sales, paid, Math.Max(0, sales - paid)); }
    private InvoiceListItem List(Invoice x) { var paid = x.Payments.Sum(p => p.Amount); return new(x.Id, x.CustomerId, $"{x.Customer.FirstName} {x.Customer.LastName}".Trim(), x.InvoiceNumber, x.Status, x.IssueDate, x.DueDate, x.TotalAmount, paid, calculator.Outstanding(x.TotalAmount, x.Payments)); }
}

public class PaymentService(SupportAgentDbContext db) : IPaymentService { public async Task<IReadOnlyList<PaymentDetail>> GetInvoicePaymentsAsync(int id, CancellationToken token = default) => await db.Payments.AsNoTracking().Where(x => x.InvoiceId == id).OrderBy(x => x.PaymentDate).Select(x => new PaymentDetail(x.Id, x.PaymentReference, x.Amount, x.PaymentDate, x.Method, x.Notes)).ToListAsync(token); }
public class InventoryService(SupportAgentDbContext db) : IInventoryService { public async Task<InventoryStatus?> GetInventoryAsync(int id, CancellationToken token = default) => await db.InventoryItems.AsNoTracking().Where(x => x.ProductId == id).Select(x => new InventoryStatus(x.ProductId, x.Product.Sku, x.Product.Name, x.QuantityOnHand, x.ReorderLevel, x.QuantityOnHand <= x.ReorderLevel)).FirstOrDefaultAsync(token); public async Task<IReadOnlyList<InventoryStatus>> GetLowStockProductsAsync(CancellationToken token = default) => await db.InventoryItems.AsNoTracking().Where(x => x.Product.IsActive && x.QuantityOnHand <= x.ReorderLevel).OrderBy(x => x.QuantityOnHand).Select(x => new InventoryStatus(x.ProductId, x.Product.Sku, x.Product.Name, x.QuantityOnHand, x.ReorderLevel, true)).ToListAsync(token); }

public class InvoiceDocumentService(IInvoiceService invoices) : IInvoiceDocumentService
{
    public async Task<byte[]?> GenerateAsync(int id, CancellationToken token = default) { GlobalFontSettings.UseWindowsFontsUnderWindows = true; var x = await invoices.GetInvoiceAsync(id, token); if (x is null) return null; using var doc = new PdfDocument(); var page = doc.AddPage(); using var g = XGraphics.FromPdfPage(page); var title = new XFont("Arial", 20, XFontStyleEx.Bold); var normal = new XFont("Arial", 10); double y = 45; g.DrawString($"INVOICE {x.InvoiceNumber}", title, XBrushes.DarkBlue, 40, y); y += 35; foreach (var line in new[] { $"Customer: {x.CustomerName}", $"Status: {x.Status}", $"Issue date: {x.IssueDate:yyyy-MM-dd}", $"Due date: {x.DueDate:yyyy-MM-dd}" }) { g.DrawString(line, normal, XBrushes.Black, 40, y); y += 18; } y += 10; foreach (var item in x.Items) { g.DrawString($"{item.Description}  {item.Quantity} x {item.UnitPrice:C}  {item.LineTotal:C}", normal, XBrushes.Black, 40, y); y += 18; } y += 15; foreach (var line in new[] { $"Subtotal: {x.Subtotal:C}", $"Tax: {x.TaxAmount:C}", $"Discount: {x.DiscountAmount:C}", $"Total: {x.TotalAmount:C}", $"Paid: {x.PaidAmount:C}", $"Outstanding: {x.OutstandingBalance:C}" }) { g.DrawString(line, normal, XBrushes.Black, 330, y); y += 18; } using var stream = new MemoryStream(); doc.Save(stream, false); return stream.ToArray(); }
}

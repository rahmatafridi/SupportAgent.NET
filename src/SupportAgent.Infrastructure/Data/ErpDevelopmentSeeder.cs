using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data;

public static class ErpDevelopmentSeeder
{
    public static async Task SeedAsync(SupportAgentDbContext db, CancellationToken ct = default)
    {
        var organizationId = TenantDefaults.DemoOrganizationId;
        var now = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var products = await db.Products.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Id).ToListAsync(ct);

        if (products.Count == 0)
        {
            products = new[]
            {
                ("SKU-1001", "Wireless Keyboard", 89m, 3m, 10m), ("SKU-1002", "Wireless Mouse", 45m, 20m, 8m),
                ("SKU-1003", "USB-C Dock", 149m, 4m, 6m), ("SKU-1004", "27-inch Monitor", 329m, 12m, 5m),
                ("SKU-1005", "Laptop Stand", 59m, 2m, 7m), ("SKU-1006", "Webcam", 99m, 18m, 5m),
                ("SKU-1007", "Headset", 79m, 6m, 6m), ("SKU-1008", "USB-C Cable", 19m, 40m, 15m),
                ("SKU-1009", "Desk Mat", 29m, 9m, 10m), ("SKU-1010", "Power Adapter", 49m, 14m, 5m)
            }.Select(x => new Product
            {
                OrganizationId = organizationId, Sku = x.Item1, Name = x.Item2, UnitPrice = x.Item3,
                IsActive = true, CreatedAt = now, UpdatedAt = now,
                Inventory = new InventoryItem { OrganizationId = organizationId, QuantityOnHand = x.Item4, ReorderLevel = x.Item5, UpdatedAt = now }
            }).ToList();
            db.Products.AddRange(products);
            await db.SaveChangesAsync(ct);
        }

        if (await db.Invoices.IgnoreQueryFilters().AnyAsync(x => x.OrganizationId == organizationId, ct)) return;
        var customers = await db.Customers.IgnoreQueryFilters()
            .Where(x => x.OrganizationId == organizationId).OrderBy(x => x.Id).Take(2).ToListAsync(ct);
        if (customers.Count < 2) return;

        Invoice Make(string number, Customer customer, string status, int productIndex, int quantity, decimal paid, DateTime issue)
        {
            var product = products[productIndex];
            var total = product.UnitPrice * quantity;
            var invoice = new Invoice
            {
                OrganizationId = organizationId, CustomerId = customer.Id, InvoiceNumber = number, Status = status,
                IssueDate = issue, DueDate = issue.AddDays(30), Subtotal = total, TotalAmount = total,
                CreatedAt = issue, UpdatedAt = issue
            };
            invoice.Items.Add(new InvoiceItem
            {
                OrganizationId = organizationId, ProductId = product.Id, Description = product.Name,
                Quantity = quantity, UnitPrice = product.UnitPrice, LineTotal = total
            });
            if (paid > 0) invoice.Payments.Add(new Payment
            {
                OrganizationId = organizationId, PaymentReference = $"PAY-{number}", Amount = paid,
                PaymentDate = issue.AddDays(5), Method = "Card", CreatedAt = issue.AddDays(5)
            });
            return invoice;
        }

        db.Invoices.AddRange(
            Make("INV-1001", customers[0], "Paid", 0, 1, 89, now),
            Make("INV-1002", customers[0], "PartiallyPaid", 3, 1, 100, now.AddDays(2)),
            Make("INV-1003", customers[1], "Issued", 2, 1, 0, now.AddDays(3)),
            Make("INV-1004", customers[1], "Overdue", 1, 2, 0, now.AddDays(-45)));
        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;

namespace SupportAgent.Infrastructure.Data;

/// <summary>
/// Seeds development-only company knowledge documents when the knowledge base is empty.
/// </summary>
public static class KnowledgeDevelopmentSeeder
{
    /// <summary>
    /// Adds sample policy documents through <see cref="IKnowledgeService"/> so chunks are created.
    /// </summary>
    /// <param name="dbContext">Database context used to detect existing knowledge documents.</param>
    /// <param name="knowledgeService">Knowledge service used to add and chunk documents.</param>
    /// <param name="cancellationToken">Token used to cancel the seed operation.</param>
    public static async Task SeedAsync(
        SupportAgentDbContext dbContext,
        IKnowledgeService knowledgeService,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.KnowledgeDocuments.AnyAsync(cancellationToken))
        {
            return;
        }

        await knowledgeService.AddDocumentAsync(
            "Refund Policy",
            """
            SupportAgent.NET offers refunds for eligible purchases within 30 days of the original order date.

            Customers may request a refund within 30 days of purchase if the order has not been fully consumed or otherwise marked non-refundable. Digital services that have already been activated, downloaded in full, or consumed beyond a reasonable trial period may not qualify for a refund.

            To request a refund, the customer should contact support with the order number and a brief explanation of the issue. Our team will review the order status, delivery confirmation, and product usage before approving or denying the request.

            Approved refunds are typically processed within 5 to 7 business days and will be returned to the original payment method whenever possible. Partial refunds may be issued when only part of an order is eligible.

            Orders marked as delivered more than 30 days ago, custom-built products, and promotional items labeled as final sale are generally not eligible for refunds unless required by local consumer protection laws.
            """,
            "internal-policy",
            cancellationToken);

        await knowledgeService.AddDocumentAsync(
            "Shipping Policy",
            """
            SupportAgent.NET ships standard orders from our primary fulfillment center Monday through Friday, excluding public holidays.

            Standard orders usually ship within 1 to 2 business days after payment confirmation. Orders placed after 2:00 PM local warehouse time may roll to the next business day. Tracking information becomes available once the carrier receives the package.

            Domestic standard shipping typically arrives within 3 to 7 business days depending on destination and carrier service level. Expedited shipping options may be available at checkout for time-sensitive orders.

            If an order has not shipped within the expected window, support should verify payment status, inventory availability, and whether the shipping address requires correction. Customers should be told that processing delays can occur during peak seasons or severe weather events.

            Once a package is handed to the carrier, delivery timelines are controlled by the carrier network. If tracking shows movement has stopped for more than 48 hours, support may open a carrier investigation or offer a replacement according to company policy.
            """,
            "internal-policy",
            cancellationToken);

        await knowledgeService.AddDocumentAsync(
            "Support Escalation Policy",
            """
            SupportAgent.NET prioritizes customer issues based on business impact, customer history, and unresolved time in queue.

            Frontline support agents should attempt standard troubleshooting first, including verifying account details, reviewing recent orders, checking shipment status, and searching the internal knowledge base for applicable policy guidance.

            High-priority unresolved customer issues should be escalated after normal troubleshooting has been completed. Escalation is appropriate when a customer reports repeated failed delivery, billing discrepancies, potential fraud, service outages, or policy exceptions that require manager approval.

            Before escalating, agents must document the customer issue, the steps already taken, relevant order or ticket identifiers, and any policy excerpts already shared with the customer. Incomplete escalations may be returned to the frontline queue.

            Tier 2 support handles complex order exceptions, carrier disputes, and policy interpretation. Tier 3 or engineering escalation is reserved for confirmed product defects, platform outages, or data integrity concerns.

            Customers should be given a realistic follow-up timeframe when an issue is escalated. Agents must not promise outcomes that are not supported by company policy or confirmed order data.
            """,
            "internal-policy",
            cancellationToken);
    }
}

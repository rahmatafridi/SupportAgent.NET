using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

internal static class ToolExecutorTestFactory
{
    public static AIToolExecutor CreateExecutor(SupportAgentDbContext context) =>
        new(
            new CustomerService(context),
            new OrderService(context),
            KnowledgeServiceTestFactory.Create(context));
}

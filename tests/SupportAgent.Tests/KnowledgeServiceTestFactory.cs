using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Knowledge;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

internal static class KnowledgeServiceTestFactory
{
    public static KnowledgeService Create(
        SupportAgentDbContext context,
        IEmbeddingService? embeddingService = null,
        KnowledgeSearchOptions? searchOptions = null,
        ICurrentUserContext? currentUser = null) =>
        new(
            context,
            new TextChunker(),
            embeddingService ?? new FakeEmbeddingService(),
            Options.Create(searchOptions ?? new KnowledgeSearchOptions()),
            NullLogger<KnowledgeService>.Instance,
            currentUser);
}

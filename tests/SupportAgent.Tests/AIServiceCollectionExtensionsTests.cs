using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.AI.Embeddings;

namespace SupportAgent.Tests;

public class AIServiceCollectionExtensionsTests
{
    [Fact]
    public void Embedding_service_uses_the_configured_typed_http_client()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:Ollama:Use"] = "true",
            ["AI:Ollama:BaseUrl"] = "http://localhost:11434",
            ["AI:Ollama:Model"] = "qwen2.5-coder:7b",
            ["AI:Embeddings:Provider"] = "Ollama",
            ["AI:Embeddings:Model"] = "nomic-embed-text"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAI(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var embeddingService = Assert.IsType<OllamaEmbeddingService>(
            scope.ServiceProvider.GetRequiredService<IEmbeddingService>());
        var httpClient = Assert.IsType<HttpClient>(typeof(OllamaEmbeddingService)
            .GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(embeddingService));

        Assert.Equal(new Uri("http://localhost:11434/"), httpClient.BaseAddress);
    }
}

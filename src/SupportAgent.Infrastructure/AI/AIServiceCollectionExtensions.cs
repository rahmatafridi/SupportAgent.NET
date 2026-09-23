using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.AI.Embeddings;
using SupportAgent.Infrastructure.AI.Providers;
using SupportAgent.Infrastructure.AI.Tools;

namespace SupportAgent.Infrastructure.AI;

/// <summary>
/// Dependency injection helpers for AI providers, the gateway, and tool execution.
/// </summary>
public static class AIServiceCollectionExtensions
{
    /// <summary>
    /// Registers AI options, providers, the gateway, and the tool executor.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">Application configuration containing the AI section.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AIOptions>()
            .Bind(configuration.GetSection(AIOptions.SectionName))
            .PostConfigure(options =>
            {
                ApplyEnvironmentOverrides(options);
                AIOptionsSelection.ApplyProviderSelection(options);
            })
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AIOptions>, AIOptionsValidator>();

        services.AddHttpClient<OllamaProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AIOptions>>().Value;
            client.BaseAddress = new Uri(options.Ollama.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<OpenAIProvider>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AIOptions>>().Value;
            client.BaseAddress = new Uri(options.OpenAI.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddHttpClient<OllamaEmbeddingService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AIOptions>>().Value;
            client.BaseAddress = new Uri(options.Ollama.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();

        services.AddSingleton<IAIProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<OllamaProvider>());
        services.AddSingleton<IAIProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenAIProvider>());
        services.AddScoped<IAIToolExecutor, AIToolExecutor>();
        services.AddScoped<IAIGateway, AIGatewayService>();

        return services;
    }

    /// <summary>
    /// Applies environment variable overrides for AI settings and secrets.
    /// </summary>
    /// <param name="options">The AI options instance loaded from configuration.</param>
    private static void ApplyEnvironmentOverrides(AIOptions options)
    {
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            options.OpenAI.ApiKey = openAiApiKey;
        }

        var provider = Environment.GetEnvironmentVariable("AI__Provider");
        if (!string.IsNullOrWhiteSpace(provider))
        {
            options.Provider = provider;
        }

        var ollamaBaseUrl = Environment.GetEnvironmentVariable("AI__Ollama__BaseUrl");
        if (!string.IsNullOrWhiteSpace(ollamaBaseUrl))
        {
            options.Ollama.BaseUrl = ollamaBaseUrl;
        }

        var ollamaModel = Environment.GetEnvironmentVariable("AI__Ollama__Model");
        if (!string.IsNullOrWhiteSpace(ollamaModel))
        {
            options.Ollama.Model = ollamaModel;
        }

        var openAiModel = Environment.GetEnvironmentVariable("AI__OpenAI__Model");
        if (!string.IsNullOrWhiteSpace(openAiModel))
        {
            options.OpenAI.Model = openAiModel;
        }

        var embeddingModel = Environment.GetEnvironmentVariable("AI__Embeddings__Model");
        if (!string.IsNullOrWhiteSpace(embeddingModel))
        {
            options.Embeddings.Model = embeddingModel;
        }
    }
}

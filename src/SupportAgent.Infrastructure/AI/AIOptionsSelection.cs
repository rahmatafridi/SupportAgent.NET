namespace SupportAgent.Infrastructure.AI;

/// <summary>
/// Resolves the active AI provider from JSON <c>Use</c> flags.
/// </summary>
public static class AIOptionsSelection
{
    /// <summary>
    /// Applies provider selection based on <see cref="OllamaOptions.Use"/> and
    /// <see cref="OpenAIOptions.Use"/>, unless <c>AI__Provider</c> is set in the environment.
    /// </summary>
    /// <param name="options">The AI options loaded from configuration.</param>
    public static void ApplyProviderSelection(AIOptions options)
    {
        var providerOverride = Environment.GetEnvironmentVariable("AI__Provider");
        if (!string.IsNullOrWhiteSpace(providerOverride))
        {
            options.Provider = providerOverride;
            return;
        }

        ApplyUseEnvironmentOverrides(options);

        if (options.Ollama.Use && options.OpenAI.Use)
        {
            throw new InvalidOperationException(
                "AI configuration is invalid: both AI:Ollama:Use and AI:OpenAI:Use are true. Set exactly one provider to true.");
        }

        if (options.Ollama.Use)
        {
            options.Provider = "Ollama";
            return;
        }

        if (options.OpenAI.Use)
        {
            options.Provider = "OpenAI";
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            options.Provider = "Ollama";
        }
    }

    private static void ApplyUseEnvironmentOverrides(AIOptions options)
    {
        var ollamaUse = ReadBoolEnvironmentVariable("AI__Ollama__Use");
        if (ollamaUse.HasValue)
        {
            options.Ollama.Use = ollamaUse.Value;
        }

        var openAiUse = ReadBoolEnvironmentVariable("AI__OpenAI__Use");
        if (openAiUse.HasValue)
        {
            options.OpenAI.Use = openAiUse.Value;
        }
    }

    private static bool? ReadBoolEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Environment variable '{name}' must be 'true' or 'false'. Current value: '{value}'.");
    }
}

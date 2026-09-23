using SupportAgent.Infrastructure.AI;

namespace SupportAgent.Tests;

public class AIOptionsSelectionTests
{
    [Fact]
    public void ApplyProviderSelection_UsesOllama_WhenOnlyOllamaUseIsTrue()
    {
        var options = new AIOptions
        {
            Ollama = { Use = true },
            OpenAI = { Use = false }
        };

        AIOptionsSelection.ApplyProviderSelection(options);

        Assert.Equal("Ollama", options.Provider);
    }

    [Fact]
    public void ApplyProviderSelection_UsesOpenAI_WhenOnlyOpenAIUseIsTrue()
    {
        var options = new AIOptions
        {
            Ollama = { Use = false },
            OpenAI = { Use = true }
        };

        AIOptionsSelection.ApplyProviderSelection(options);

        Assert.Equal("OpenAI", options.Provider);
    }

    [Fact]
    public void ApplyProviderSelection_Throws_WhenBothUseFlagsAreTrue()
    {
        var options = new AIOptions
        {
            Ollama = { Use = true },
            OpenAI = { Use = true }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AIOptionsSelection.ApplyProviderSelection(options));

        Assert.Contains("exactly one provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyProviderSelection_KeepsProvider_WhenBothUseFlagsAreFalse()
    {
        var options = new AIOptions
        {
            Provider = "OpenAI",
            Ollama = { Use = false },
            OpenAI = { Use = false }
        };

        AIOptionsSelection.ApplyProviderSelection(options);

        Assert.Equal("OpenAI", options.Provider);
    }
}

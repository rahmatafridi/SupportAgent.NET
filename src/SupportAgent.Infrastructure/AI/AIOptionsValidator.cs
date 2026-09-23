using Microsoft.Extensions.Options;

namespace SupportAgent.Infrastructure.AI;

/// <summary>
/// Validates AI configuration on application startup.
/// </summary>
public sealed class AIOptionsValidator : IValidateOptions<AIOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AIOptions options)
    {
        if (options.Ollama.Use && options.OpenAI.Use)
        {
            return ValidateOptionsResult.Fail(
                "AI configuration is invalid: both AI:Ollama:Use and AI:OpenAI:Use are true. Set exactly one provider to true.");
        }

        if (string.Equals(options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.OpenAI.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                "AI configuration is invalid: OpenAI is selected but AI:OpenAI:ApiKey is empty. Set the API key or set AI:OpenAI:Use to false.");
        }

        return ValidateOptionsResult.Success;
    }
}

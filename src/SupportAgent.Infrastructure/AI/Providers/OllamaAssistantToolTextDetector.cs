using System.Text.Json;
using System.Text.RegularExpressions;

namespace SupportAgent.Infrastructure.AI.Providers;

/// <summary>
/// Detects assistant text that looks like a tool call without using Ollama native tool_calls.
/// </summary>
internal static partial class OllamaAssistantToolTextDetector
{
    /// <summary>
    /// Returns true when assistant content appears to contain a non-native tool request.
    /// </summary>
    /// <param name="content">Assistant message content from Ollama.</param>
    /// <returns><c>true</c> when the text resembles a tool call written into content.</returns>
    public static bool LooksLikeNonNativeToolCallText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.Trim();

        if (ToolCallTagRegex().IsMatch(trimmed))
        {
            return true;
        }

        return LooksLikeJsonToolCallObject(trimmed);
    }

    private static bool LooksLikeJsonToolCallObject(string content)
    {
        if (!content.StartsWith('{') || !content.EndsWith('}'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (!root.TryGetProperty("name", out var nameProperty) ||
                !root.TryGetProperty("arguments", out _))
            {
                return false;
            }

            return nameProperty.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(nameProperty.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"<tool_call>\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ToolCallTagRegex();
}

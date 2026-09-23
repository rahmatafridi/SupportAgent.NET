using System.Text.Json;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.AI.Tools;

/// <summary>
/// Extracts grounded knowledge sources from SearchKnowledgeBase tool results.
/// </summary>
internal static class KnowledgeSourceParser
{
    /// <summary>
    /// Adds deduplicated knowledge sources from a successful SearchKnowledgeBase tool result.
    /// </summary>
    /// <param name="toolName">The executed tool name.</param>
    /// <param name="content">The JSON tool result content.</param>
    /// <param name="sources">The collection of sources to append to.</param>
    public static void TryAddSourcesFromToolResult(
        string toolName,
        string content,
        ICollection<KnowledgeSource> sources)
    {
        if (!string.Equals(toolName, SupportToolDefinitions.SearchKnowledgeBaseToolName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
            if (!document.RootElement.TryGetProperty("success", out var successElement) ||
                !successElement.GetBoolean())
            {
                return;
            }

            if (!document.RootElement.TryGetProperty("results", out var resultsElement))
            {
                return;
            }

            foreach (var result in resultsElement.EnumerateArray())
            {
                if (!result.TryGetProperty("documentTitle", out var titleElement))
                {
                    continue;
                }

                var title = titleElement.GetString();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                string? source = null;
                if (result.TryGetProperty("documentSource", out var sourceElement) &&
                    sourceElement.ValueKind == JsonValueKind.String)
                {
                    source = sourceElement.GetString();
                }

                if (sources.Any(existing =>
                        string.Equals(existing.Title, title, StringComparison.Ordinal) &&
                        string.Equals(existing.Source, source, StringComparison.Ordinal)))
                {
                    continue;
                }

                sources.Add(new KnowledgeSource
                {
                    Title = title,
                    Source = source
                });
            }
        }
        catch (JsonException)
        {
            // Ignore malformed tool payloads; the model still receives the raw tool result.
        }
    }
}

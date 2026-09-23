using System.Text.Json;
using System.Text.RegularExpressions;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Copilot;

/// <summary>
/// Parses structured draft reply JSON from model output.
/// </summary>
public static partial class CopilotDraftReplyParser
{
    /// <summary>
    /// Parses structured draft reply JSON from assistant text.
    /// </summary>
    public static CopilotDraftReply Parse(string assistantText)
    {
        var json = ExtractJsonObject(assistantText);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return new CopilotDraftReply
            {
                Subject = ReadString(root, "subject"),
                Body = ReadString(root, "body"),
                Tone = ReadString(root, "tone", "professional"),
                Confidence = ReadConfidence(root)
            };
        }
        catch (JsonException)
        {
            return new CopilotDraftReply
            {
                Subject = string.Empty,
                Body = assistantText.Trim(),
                Tone = "professional",
                Confidence = 0.5
            };
        }
    }

    private static string ExtractJsonObject(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var match = JsonBlockRegex().Match(trimmed);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return trimmed;
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback = "")
    {
        if (root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? fallback;
        }

        return fallback;
    }

    private static double ReadConfidence(JsonElement root)
    {
        if (!root.TryGetProperty("confidence", out var property))
        {
            return 0.0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDouble(out var value) => Math.Clamp(value, 0, 1),
            JsonValueKind.String when double.TryParse(property.GetString(), out var parsed) => Math.Clamp(parsed, 0, 1),
            _ => 0.0
        };
    }

    [GeneratedRegex(@"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonBlockRegex();
}

using System.Text.Json;
using System.Text.RegularExpressions;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Copilot;

internal record ParsedSuggestedAction(string Label, string? Description, AISuggestedActionType ActionType);

internal record ParsedSupportResponse(
    string Answer,
    IReadOnlyList<ParsedSuggestedAction> SuggestedActions,
    double Confidence,
    bool StructuredOutputFailed = false);

internal static partial class CopilotSupportResponseParser
{
    private const string SafeFormattingFailureAnswer =
        "The AI response could not be formatted safely. Please try again.";

    public static ParsedSupportResponse Parse(string text)
    {
        var trimmed = text.Trim();
        var json = ExtractJsonObject(trimmed);

        if (json is null)
        {
            return LooksLikeStructuredOutput(trimmed)
                ? new(SafeFormattingFailureAnswer, [], 0, true)
                : new(trimmed, [], 0.5);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("answer", out var answerNode) ||
                answerNode.ValueKind != JsonValueKind.String)
            {
                return new(SafeFormattingFailureAnswer, [], 0, true);
            }

            var answer = answerNode.GetString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(answer) || LooksLikeStructuredOutput(answer))
            {
                return new(SafeFormattingFailureAnswer, [], 0, true);
            }

            var actions = ReadActions(root);
            var confidence = root.TryGetProperty("confidence", out var confidenceNode) && confidenceNode.TryGetDouble(out var value) ? Math.Clamp(value, 0, 1) : 0;
            return new(answer, actions, confidence);
        }
        catch (JsonException)
        {
            return new(SafeFormattingFailureAnswer, [], 0, true);
        }
    }

    private static IReadOnlyList<ParsedSuggestedAction> ReadActions(JsonElement root)
    {
        if (!root.TryGetProperty("suggestedActions", out var node) || node.ValueKind != JsonValueKind.Array)
            return [];
        var actions = new List<ParsedSuggestedAction>();
        foreach (var item in node.EnumerateArray().Take(5))
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("label", out var labelNode) || labelNode.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("actionType", out var typeNode) || typeNode.ValueKind != JsonValueKind.String ||
                !Enum.TryParse<AISuggestedActionType>(typeNode.GetString(), true, out var actionType)) continue;
            var label = labelNode.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(label)) continue;
            var description = item.TryGetProperty("description", out var descriptionNode) && descriptionNode.ValueKind == JsonValueKind.String
                ? descriptionNode.GetString()?.Trim() : null;
            actions.Add(new ParsedSuggestedAction(label, description, actionType));
        }
        return actions;
    }

    private static string? ExtractJsonObject(string text)
    {
        if (text.StartsWith('{') && text.EndsWith('}'))
        {
            return text;
        }

        var match = JsonBlockRegex().Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static bool LooksLikeStructuredOutput(string text) =>
        text.StartsWith('{') ||
        text.StartsWith("```", StringComparison.Ordinal) ||
        text.Contains("\"answer\"", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("suggestedActions", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"```(?:json)?\s*(\{[\s\S]*\})\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonBlockRegex();
}

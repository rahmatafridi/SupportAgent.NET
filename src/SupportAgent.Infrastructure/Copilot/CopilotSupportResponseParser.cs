using System.Text.Json;

namespace SupportAgent.Infrastructure.Copilot;

internal record ParsedSupportResponse(string Answer, IReadOnlyList<string> SuggestedActions, double Confidence);

internal static class CopilotSupportResponseParser
{
    public static ParsedSupportResponse Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text.Trim());
            var root = document.RootElement;
            var answer = root.TryGetProperty("answer", out var answerNode) ? answerNode.GetString() ?? string.Empty : string.Empty;
            var actions = root.TryGetProperty("suggestedActions", out var actionsNode) && actionsNode.ValueKind == JsonValueKind.Array
                ? actionsNode.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => !string.IsNullOrWhiteSpace(x)).Take(5).ToList()
                : [];
            var confidence = root.TryGetProperty("confidence", out var confidenceNode) && confidenceNode.TryGetDouble(out var value) ? Math.Clamp(value, 0, 1) : 0;
            return string.IsNullOrWhiteSpace(answer) ? new(text.Trim(), [], 0.5) : new(answer, actions, confidence);
        }
        catch (JsonException) { return new(text.Trim(), [], 0.5); }
    }
}

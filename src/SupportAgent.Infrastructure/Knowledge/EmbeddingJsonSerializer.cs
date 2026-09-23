using System.Text.Json;

namespace SupportAgent.Infrastructure.Knowledge;

/// <summary>
/// Serializes embedding vectors to and from JSON for SQL Server storage.
/// </summary>
public static class EmbeddingJsonSerializer
{
    /// <summary>
    /// Serializes one embedding vector to JSON.
    /// </summary>
    /// <param name="embedding">The embedding vector.</param>
    /// <returns>JSON array text suitable for persistence.</returns>
    public static string Serialize(IReadOnlyList<float> embedding) =>
        JsonSerializer.Serialize(embedding);

    /// <summary>
    /// Deserializes one embedding vector from JSON.
    /// </summary>
    /// <param name="embeddingJson">The stored JSON array text.</param>
    /// <returns>The embedding vector, or null when missing or invalid.</returns>
    public static float[]? Deserialize(string? embeddingJson)
    {
        if (string.IsNullOrWhiteSpace(embeddingJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<float[]>(embeddingJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

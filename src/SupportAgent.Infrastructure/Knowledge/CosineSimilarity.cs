namespace SupportAgent.Infrastructure.Knowledge;

/// <summary>
/// Cosine similarity calculations for embedding vectors.
/// </summary>
public static class CosineSimilarity
{
    /// <summary>
    /// Calculates cosine similarity between two vectors.
    /// </summary>
    /// <param name="left">The first embedding vector.</param>
    /// <param name="right">The second embedding vector.</param>
    /// <returns>Similarity between 0 and 1 for normalized vectors.</returns>
    public static double Calculate(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0;
        }

        double dot = 0;
        double normLeft = 0;
        double normRight = 0;

        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            normLeft += left[index] * left[index];
            normRight += right[index] * right[index];
        }

        if (normLeft == 0 || normRight == 0)
        {
            return 0;
        }

        var similarity = dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight));
        return Math.Clamp(similarity, 0, 1);
    }
}

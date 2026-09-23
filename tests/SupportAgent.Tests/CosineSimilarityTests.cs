using SupportAgent.Infrastructure.Knowledge;

namespace SupportAgent.Tests;

public class CosineSimilarityTests
{
    [Fact]
    public void Calculate_ReturnsOne_ForIdenticalVectors()
    {
        var vector = new float[] { 0.6f, 0.8f };

        var similarity = CosineSimilarity.Calculate(vector, vector);

        Assert.Equal(1, similarity, precision: 5);
    }

    [Fact]
    public void Calculate_ReturnsZero_ForOrthogonalVectors()
    {
        var left = new float[] { 1f, 0f };
        var right = new float[] { 0f, 1f };

        var similarity = CosineSimilarity.Calculate(left, right);

        Assert.Equal(0, similarity, precision: 5);
    }

    [Fact]
    public void Calculate_ReturnsZero_WhenVectorLengthsDiffer()
    {
        var left = new float[] { 1f, 0f };
        var right = new float[] { 1f, 0f, 0f };

        var similarity = CosineSimilarity.Calculate(left, right);

        Assert.Equal(0, similarity);
    }
}

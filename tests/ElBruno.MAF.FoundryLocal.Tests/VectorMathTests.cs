using ElBruno.MAF.FoundryLocal;

namespace ElBruno.MAF.FoundryLocal.Tests;

public class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_ReturnsOneForIdenticalVectors()
    {
        var similarity = VectorMath.CosineSimilarity([1f, 2f, 3f], [1f, 2f, 3f]);

        Assert.Equal(1d, similarity, 6);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZeroWhenOneVectorIsZero()
    {
        var similarity = VectorMath.CosineSimilarity([0f, 0f, 0f], [1f, 2f, 3f]);

        Assert.Equal(0d, similarity, 6);
    }

    [Fact]
    public void CosineSimilarity_ThrowsForDifferentDimensions()
    {
        Assert.Throws<ArgumentException>(() => VectorMath.CosineSimilarity([1f, 2f], [1f]));
    }
}

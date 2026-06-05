namespace ElBruno.MAF.FoundryLocal;

public static class VectorMath
{
    public static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Count == 0 || right.Count == 0)
        {
            throw new ArgumentException("Vectors must not be empty.");
        }

        if (left.Count != right.Count)
        {
            throw new ArgumentException("Vectors must have the same dimensions.");
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm <= double.Epsilon || rightNorm <= double.Epsilon)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}

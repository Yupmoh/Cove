using System.Numerics;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class EmbeddingResult(float[] Vector)
{
    public float[] Vector { get; } = Vector;
    public int Dimensions => Vector.Length;
}

public sealed class SemanticEmbedder
{
    private readonly ILogger _logger;
    private readonly int _dimensions;

    public SemanticEmbedder(ILogger logger, int dimensions = 256)
    {
        _logger = logger;
        _dimensions = dimensions;
    }

    public EmbeddingResult Embed(string text)
    {
        var tokens = Tokenize(text);
        var vector = new float[_dimensions];

        foreach (var token in tokens)
        {
            var hash = HashToken(token);
            var idx = (int)(hash % (uint)_dimensions);
            vector[idx] += 1.0f;
        }

        Normalize(vector);
        return new EmbeddingResult(vector);
    }

    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, magA = 0, magB = 0;
        int i = 0;
        int simdLength = System.Numerics.Vector<float>.Count;
        for (; i <= a.Length - simdLength; i += simdLength)
        {
            var va = new System.Numerics.Vector<float>(a, i);
            var vb = new System.Numerics.Vector<float>(b, i);
            dot += System.Numerics.Vector.Dot(va, vb);
            magA += System.Numerics.Vector.Dot(va, va);
            magB += System.Numerics.Vector.Dot(vb, vb);
        }
        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (System.Math.Sqrt(magA) * System.Math.Sqrt(magB));
    }

    private static System.Collections.Generic.IReadOnlyList<string> Tokenize(string text)
    {
        var result = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
                current.Append(char.ToLowerInvariant(c));
            else if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }

    private static uint HashToken(string token)
    {
        uint hash = 2166136261;
        foreach (var c in token)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    private static void Normalize(float[] vector)
    {
        float mag = 0;
        foreach (var v in vector)
            mag += v * v;
        mag = System.MathF.Sqrt(mag);
        if (mag == 0) return;
        for (int i = 0; i < vector.Length; i++)
            vector[i] /= mag;
    }
}

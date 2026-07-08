using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SemanticEmbedderTests
{
    private readonly SemanticEmbedder _embedder = new(NullLogger.Instance, dimensions: 64);

    [Fact]
    public void Embed_ProducesNormalizedVector()
    {
        var result = _embedder.Embed("hello world test");
        Assert.Equal(64, result.Dimensions);

        float mag = 0;
        foreach (var v in result.Vector)
            mag += v * v;
        mag = System.MathF.Sqrt(mag);
        Assert.Equal(1.0f, mag, 0.01f);
    }

    [Fact]
    public void Embed_SimilarTexts_HaveHighCosineSimilarity()
    {
        var v1 = _embedder.Embed("the routing module handles dispatch");
        var v2 = _embedder.Embed("the routing module handles dispatch");
        var sim = SemanticEmbedder.CosineSimilarity(v1.Vector, v2.Vector);
        Assert.Equal(1.0, sim, 0.01);
    }

    [Fact]
    public void Embed_DifferentTexts_HaveLowerSimilarity()
    {
        var v1 = _embedder.Embed("the routing module handles dispatch");
        var v2 = _embedder.Embed("completely unrelated content about cooking");
        var sim = SemanticEmbedder.CosineSimilarity(v1.Vector, v2.Vector);
        Assert.True(sim < 0.9);
    }

    [Fact]
    public void Embed_PartiallySharedTokens_HaveModerateSimilarity()
    {
        var v1 = _embedder.Embed("the routing module handles dispatch");
        var v2 = _embedder.Embed("the routing system manages dispatch");
        var sim = SemanticEmbedder.CosineSimilarity(v1.Vector, v2.Vector);
        Assert.True(sim > 0.3);
        Assert.True(sim < 1.0);
    }

    [Fact]
    public void Embed_Deterministic_SameInputSameOutput()
    {
        var v1 = _embedder.Embed("deterministic test input");
        var v2 = _embedder.Embed("deterministic test input");
        Assert.Equal(v1.Vector, v2.Vector);
    }

    [Fact]
    public void Embed_EmptyString_ProducesZeroVector()
    {
        var result = _embedder.Embed("");
        Assert.Equal(64, result.Dimensions);
        float mag = 0;
        foreach (var v in result.Vector)
            mag += v * v;
        Assert.Equal(0.0f, mag);
    }

    [Fact]
    public void Embed_CaseInsensitive()
    {
        var v1 = _embedder.Embed("Hello World");
        var v2 = _embedder.Embed("hello world");
        var sim = SemanticEmbedder.CosineSimilarity(v1.Vector, v2.Vector);
        Assert.Equal(1.0, sim, 0.01);
    }

    [Fact]
    public void CosineSimilarity_DifferentDimensions_ReturnsZero()
    {
        var v1 = new float[] { 1, 0, 0 };
        var v2 = new float[] { 1, 0 };
        var sim = SemanticEmbedder.CosineSimilarity(v1, v2);
        Assert.Equal(0.0, sim);
    }
}

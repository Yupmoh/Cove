using Cove.Dictation;
using Xunit;

namespace Cove.Dictation.Tests;

public sealed class AudioResamplerTests
{
    [Fact]
    public void SameRate_IsPassthrough()
    {
        var src = new float[] { 0.1f, 0.2f, 0.3f };
        Assert.Same(src, AudioResampler.Resample(src, 16000, 16000));
    }

    [Fact]
    public void FortyEightToSixteen_ThirdsTheLength()
    {
        var src = new float[48000];
        var result = AudioResampler.Resample(src, 48000, 16000);
        Assert.Equal(16000, result.Length);
    }

    [Fact]
    public void FortyEightToSixteen_PreservesToneFrequency()
    {
        const int from = 48000, to = 16000;
        const double hz = 440;
        var src = new float[from];
        for (var i = 0; i < src.Length; i++)
            src[i] = (float)Math.Sin(2 * Math.PI * hz * i / from);
        var result = AudioResampler.Resample(src, from, to);

        var crossings = 0;
        for (var i = 1; i < result.Length; i++)
            if (result[i - 1] < 0 != result[i] < 0)
                crossings++;
        var measuredHz = crossings / 2.0;
        Assert.InRange(measuredHz, hz - 5, hz + 5);
    }

    [Fact]
    public void FortyEightToSixteen_AttenuatesOutOfBandTone()
    {
        const int from = 48000, to = 16000;
        const double hz = 12000;
        var src = new float[from];
        for (var i = 0; i < src.Length; i++)
            src[i] = (float)Math.Sin(2 * Math.PI * hz * i / from);
        var result = AudioResampler.Resample(src, from, to);

        double inputRms = 0, outputRms = 0;
        foreach (var s in src)
            inputRms += s * s;
        foreach (var s in result)
            outputRms += s * s;
        inputRms = Math.Sqrt(inputRms / src.Length);
        outputRms = Math.Sqrt(outputRms / result.Length);

        Assert.True(outputRms < inputRms * 0.1, $"aliased energy too high: {outputRms / inputRms:F3}");
    }

    [Fact]
    public void Empty_StaysEmpty()
    {
        Assert.Empty(AudioResampler.Resample([], 48000, 16000));
    }
}

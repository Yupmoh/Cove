namespace Cove.Dictation;

public static class AudioResampler
{
    private const int FirTaps = 63;

    public static float[] Resample(float[] source, int fromRate, int toRate)
    {
        if (fromRate <= 0 || toRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(fromRate), "sample rates must be positive");
        if (fromRate == toRate || source.Length == 0)
            return source;
        var filtered = fromRate > toRate ? LowPass(source, fromRate, toRate) : source;
        var length = (int)((long)filtered.Length * toRate / fromRate);
        if (length == 0)
            return [];
        var result = new float[length];
        var step = (double)fromRate / toRate;
        for (var i = 0; i < length; i++)
        {
            var pos = i * step;
            var left = (int)pos;
            var right = Math.Min(left + 1, filtered.Length - 1);
            var frac = (float)(pos - left);
            result[i] = filtered[left] + (filtered[right] - filtered[left]) * frac;
        }
        return result;
    }

    private static float[] LowPass(float[] source, int fromRate, int toRate)
    {
        var cutoff = 0.45 * toRate / fromRate;
        var taps = new float[FirTaps];
        var center = FirTaps / 2;
        double sum = 0;
        for (var i = 0; i < FirTaps; i++)
        {
            var n = i - center;
            var sinc = n == 0 ? 2 * cutoff : Math.Sin(2 * Math.PI * cutoff * n) / (Math.PI * n);
            var window = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (FirTaps - 1));
            taps[i] = (float)(sinc * window);
            sum += taps[i];
        }
        for (var i = 0; i < FirTaps; i++)
            taps[i] = (float)(taps[i] / sum);

        var filtered = new float[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            float acc = 0;
            for (var t = 0; t < FirTaps; t++)
            {
                var idx = i - center + t;
                if ((uint)idx < (uint)source.Length)
                    acc += source[idx] * taps[t];
            }
            filtered[i] = acc;
        }
        return filtered;
    }
}

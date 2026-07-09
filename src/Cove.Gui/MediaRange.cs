namespace Cove.Gui;

public readonly record struct MediaRangeResult(int StatusCode, long Start, long End, long Length, long TotalLength)
{
    public bool IsPartial => StatusCode == 206;
}

public static class MediaRange
{
    private const string BytesUnit = "bytes=";

    public static MediaRangeResult Resolve(string? rangeHeader, long totalLength)
    {
        var whole = new MediaRangeResult(200, 0, totalLength - 1, totalLength, totalLength);

        if (string.IsNullOrWhiteSpace(rangeHeader))
            return whole;

        var trimmed = rangeHeader.Trim();
        if (!trimmed.StartsWith(BytesUnit, StringComparison.OrdinalIgnoreCase))
            return whole;

        var spec = trimmed[BytesUnit.Length..].Trim();
        if (spec.Contains(','))
            return whole;

        var dash = spec.IndexOf('-');
        if (dash < 0)
            return Unsatisfiable(totalLength);

        var startPart = spec[..dash].Trim();
        var endPart = spec[(dash + 1)..].Trim();

        if (startPart.Length == 0)
        {
            if (!long.TryParse(endPart, out var suffix) || suffix <= 0 || totalLength <= 0)
                return Unsatisfiable(totalLength);
            var start = suffix >= totalLength ? 0 : totalLength - suffix;
            return Partial(start, totalLength - 1, totalLength);
        }

        if (!long.TryParse(startPart, out var from) || from < 0 || from >= totalLength)
            return Unsatisfiable(totalLength);

        long to;
        if (endPart.Length == 0)
        {
            to = totalLength - 1;
        }
        else
        {
            if (!long.TryParse(endPart, out to) || to < from)
                return Unsatisfiable(totalLength);
            if (to >= totalLength) to = totalLength - 1;
        }

        return Partial(from, to, totalLength);
    }

    private static MediaRangeResult Partial(long start, long end, long totalLength)
        => new(206, start, end, end - start + 1, totalLength);

    private static MediaRangeResult Unsatisfiable(long totalLength)
        => new(416, 0, -1, 0, totalLength);
}

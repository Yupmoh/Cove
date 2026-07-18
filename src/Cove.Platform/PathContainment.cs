namespace Cove.Platform;

public static class PathContainment
{
    public static bool IsContained(string root, string candidate)
    {
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(candidate))
            return false;

        string canonicalRoot;
        string canonicalCandidate;
        try
        {
            canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            canonicalCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(canonicalRoot, canonicalCandidate, comparison))
            return true;

        var prefix = Path.EndsInDirectorySeparator(canonicalRoot)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;
        return canonicalCandidate.StartsWith(prefix, comparison);
    }

    public static bool TryResolveContained(string root, out string resolvedRoot, out string resolvedCandidate, params string[] segments)
    {
        resolvedRoot = string.Empty;
        resolvedCandidate = string.Empty;
        if (string.IsNullOrEmpty(root))
            return false;

        try
        {
            resolvedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var combined = resolvedRoot;
            foreach (var segment in segments)
                combined = Path.Combine(combined, segment ?? string.Empty);
            resolvedCandidate = Path.GetFullPath(combined);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return IsContained(resolvedRoot, resolvedCandidate);
    }

    public static bool IsSafeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;
        if (segment is "." or "..")
            return false;
        if (Path.IsPathRooted(segment))
            return false;
        foreach (var ch in segment)
        {
            if (ch == '/' || ch == '\\')
                return false;
        }
        return segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}

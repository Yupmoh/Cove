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

    public static bool IsContainedPhysical(string root, string candidate)
    {
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(candidate))
            return false;
        try
        {
            return IsContained(ResolvePhysical(root), ResolvePhysical(candidate));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string ResolvePhysical(string path)
        => ResolvePhysicalCore(path, 0);

    private static string ResolvePhysicalCore(string path, int depth)
    {
        if (depth > 10)
            throw new IOException($"symlink resolution exceeded depth limit for '{path}'");
        var full = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(pathRoot))
            return full;
        var current = pathRoot;
        var components = full[pathRoot.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var missingTail = false;
        foreach (var component in components)
        {
            current = Path.Combine(current, component);
            if (missingTail)
                continue;
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (!info.Exists)
            {
                var entryLink = new FileInfo(current).LinkTarget ?? new DirectoryInfo(current).LinkTarget;
                if (entryLink is null)
                {
                    missingTail = true;
                    continue;
                }
                var baseDir = Path.GetDirectoryName(current) ?? pathRoot;
                current = ResolvePhysicalCore(Path.IsPathRooted(entryLink) ? entryLink : Path.Combine(baseDir, entryLink), depth + 1);
                continue;
            }
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
                current = ResolvePhysicalCore(target.FullName, depth + 1);
        }
        return current;
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

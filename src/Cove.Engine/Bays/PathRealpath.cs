namespace Cove.Engine.Bays;

public static class PathRealpath
{
    private const int MaxSymlinkDepth = 40;

    public static string Normalize(string path) => Normalize(path, 0);

    private static string Normalize(string path, int depth)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root))
            return full;
        var current = root;
        var remainder = full[root.Length..]
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in remainder)
        {
            current = Path.Combine(current, part);
            try
            {
                if (File.ResolveLinkTarget(current, true) is { } target)
                    current = depth < MaxSymlinkDepth ? Normalize(target.FullName, depth + 1) : target.FullName;
            }
            catch (DirectoryNotFoundException) { }
            catch (FileNotFoundException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return current;
    }
}

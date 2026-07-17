namespace Cove.Engine.Bays;

public static class WorktreePattern
{
    public static string Expand(string pattern, string repo, string branch)
    {
        if (string.IsNullOrEmpty(pattern))
            return pattern;
        return pattern.Replace("{repo}", repo).Replace("{branch}", branch);
    }

    public static string DeriveRepoName(string projectDir)
    {
        if (string.IsNullOrEmpty(projectDir))
            return "repo";
        var trimmed = projectDir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var name = System.IO.Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? "repo" : name;
    }

    public static string ResolveLocation(string location, string parentProjectDir)
    {
        if (System.IO.Path.IsPathRooted(location))
            return System.IO.Path.GetFullPath(location);
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(parentProjectDir, location));
    }
}

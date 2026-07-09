namespace Cove.Adapters;

public static class BashLocator
{
    public static string? Find()
    {
        if (!OperatingSystem.IsWindows())
            return File.Exists("/bin/bash") ? "/bin/bash" : File.Exists("/usr/bin/bash") ? "/usr/bin/bash" : null;

        foreach (var root in WindowsGitRoots())
        {
            var candidate = Path.Combine(root, "Git", "bin", "bash.exe");
            if (File.Exists(candidate)) return candidate;
        }

        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in pathDirs)
        {
            if (dir.StartsWith(system32, StringComparison.OrdinalIgnoreCase)) continue;
            var candidate = Path.Combine(dir, "bash.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> WindowsGitRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
    }
}

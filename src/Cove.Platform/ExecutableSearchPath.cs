namespace Cove.Platform;

public sealed class ExecutableSearchPath(IRuntimeEnvironment environment)
{
    private static readonly string[] DefaultWindowsExtensions = [".exe", ".com", ".cmd", ".bat"];

    public IReadOnlyList<string> ExecutableExtensions
    {
        get
        {
            if (!environment.IsWindows)
                return [];
            if (string.IsNullOrWhiteSpace(environment.PathExtensions))
                return DefaultWindowsExtensions;

            var extensions = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in environment.PathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var value = entry.Trim();
                if (value.Length == 0)
                    continue;
                var extension = (value.StartsWith('.') ? value : "." + value).ToLowerInvariant();
                if (seen.Add(extension))
                    extensions.Add(extension);
            }
            return extensions.Count == 0 ? DefaultWindowsExtensions : extensions;
        }
    }

    public IReadOnlyList<string> Resolve(
        string? loginShellPath,
        IReadOnlyList<string> adapterSpecificRoots)
    {
        var directories = new List<string>();
        var seen = new HashSet<string>(
            environment.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        AddPathEntries(directories, seen, loginShellPath);
        AddPathEntries(directories, seen, environment.ExecutablePath);
        if (environment.IsWindows)
        {
            AddPathEntries(directories, seen, environment.UserExecutablePath);
            AddPathEntries(directories, seen, environment.MachineExecutablePath);
        }

        Add(directories, seen, environment.GetEnvironmentVariable("NVM_BIN"));
        Add(directories, seen, environment.GetEnvironmentVariable("PNPM_HOME"));
        AddCombined(directories, seen, environment.GetEnvironmentVariable("VOLTA_HOME"), "bin");
        AddCombined(directories, seen, environment.GetEnvironmentVariable("FNM_MULTISHELL_PATH"), "bin");

        if (environment.IsWindows)
        {
            AddCombined(directories, seen, environment.GetEnvironmentVariable("APPDATA"), "npm");
            AddCombined(directories, seen, environment.GetEnvironmentVariable("USERPROFILE"), ".bun", "bin");
            AddCombined(directories, seen, environment.GetEnvironmentVariable("LOCALAPPDATA"), "pnpm");
            AddCombined(directories, seen, environment.GetEnvironmentVariable("VOLTA_HOME"), "bin");
            AddCombined(directories, seen, environment.GetEnvironmentVariable("USERPROFILE"), "scoop", "shims");
            AddCombined(directories, seen, environment.GetEnvironmentVariable("LOCALAPPDATA"), "Microsoft", "WinGet", "Links");
        }
        else if (environment.IsMacOS)
        {
            AddCombined(directories, seen, environment.HomeDirectory, ".bun", "bin");
            AddCombined(directories, seen, environment.HomeDirectory, ".local", "bin");
            Add(directories, seen, "/opt/homebrew/bin");
            Add(directories, seen, "/usr/local/bin");
        }
        else if (environment.IsLinux)
        {
            AddCombined(directories, seen, environment.HomeDirectory, ".bun", "bin");
            AddCombined(directories, seen, environment.HomeDirectory, ".local", "bin");
            AddCombined(directories, seen, environment.HomeDirectory, ".linuxbrew", "bin");
            Add(directories, seen, "/home/linuxbrew/.linuxbrew/bin");
            Add(directories, seen, "/usr/local/bin");
            Add(directories, seen, "/usr/bin");
        }

        foreach (var root in adapterSpecificRoots)
            Add(directories, seen, root);

        return directories;
    }

    private void AddPathEntries(
        List<string> directories,
        HashSet<string> seen,
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        var separator = environment.IsWindows ? ';' : ':';
        foreach (var entry in path.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            Add(directories, seen, entry);
    }

    private void AddCombined(
        List<string> directories,
        HashSet<string> seen,
        string? parent,
        params string[] children)
    {
        if (string.IsNullOrWhiteSpace(parent))
            return;
        Add(directories, seen, Path.Combine([parent, .. children]));
    }

    private void Add(
        List<string> directories,
        HashSet<string> seen,
        string? directory)
    {
        var normalized = Normalize(directory);
        if (normalized is not null && seen.Add(normalized))
            directories.Add(normalized);
    }

    private string? Normalize(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;
        var expanded = ExpandHome(directory.Trim());
        var root = Path.GetPathRoot(expanded);
        var normalized = expanded.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.Length == 0)
            return root ?? expanded;
        if (root is not null
            && string.Equals(
                normalized,
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                environment.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return root;
        }
        return normalized;
    }

    private string ExpandHome(string path)
    {
        if (path == "~")
            return environment.HomeDirectory;
        if (path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return Path.Combine(environment.HomeDirectory, path[2..]);
        }
        return path;
    }
}

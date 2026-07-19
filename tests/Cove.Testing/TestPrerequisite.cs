using Xunit.Sdk;

namespace Cove.Testing;

public static class TestPlatform
{
    public static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new XunitException("Requires Windows");
    }

    public static void RequireMacOS()
    {
        if (!OperatingSystem.IsMacOS())
            throw new XunitException("Requires macOS");
    }

    public static void RequireLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new XunitException("Requires Linux");
    }

    public static void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
            throw new XunitException("Requires a Unix platform");
    }
}

public static class TestPrerequisite
{
    public static void RequireFlag(string variable)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(variable), "1", StringComparison.Ordinal))
            throw new XunitException($"Requires {variable}=1");
    }

    public static string RequireEnvironmentVariable(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
            throw new XunitException($"Requires environment variable {variable}");
        return value!;
    }

    public static string RequireFile(string path, string? requirement = null)
    {
        if (!File.Exists(path))
            throw new XunitException(requirement ?? $"Requires file {path}");
        return path;
    }

    public static string RequireDirectory(string path, string? requirement = null)
    {
        if (!Directory.Exists(path))
            throw new XunitException(requirement ?? $"Requires directory {path}");
        return path;
    }

    public static string RequireExecutable(string name)
    {
        var executable = FindExecutable(name);
        if (executable is null)
            throw new XunitException($"Requires executable {name}");
        return executable;
    }

    public static string RequireAnyExecutable(params string[] names)
    {
        foreach (var name in names)
        {
            var executable = FindExecutable(name);
            if (executable is not null)
                return executable;
        }
        throw new XunitException($"Requires one executable: {string.Join(", ", names)}");
    }

    public static string? FindExecutable(string name)
    {
        if (Path.IsPathFullyQualified(name))
            return File.Exists(name) ? name : null;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, name + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}

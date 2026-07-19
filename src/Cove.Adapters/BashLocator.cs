using Cove.Platform;

namespace Cove.Adapters;

public interface IBashLocator
{
    string? Find();
}

public sealed class BashLocator : IBashLocator
{
    private readonly IPlatformFileSystem _fileSystem;
    private readonly IRuntimeEnvironment _environment;

    public BashLocator(
        IPlatformFileSystem? fileSystem = null,
        IRuntimeEnvironment? environment = null)
    {
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
        _environment = environment ?? SystemRuntimeEnvironment.Instance;
    }

    public string? Find()
    {
        if (!_environment.IsWindows)
        {
            if (_fileSystem.FileExists("/bin/bash"))
                return "/bin/bash";
            return _fileSystem.FileExists("/usr/bin/bash") ? "/usr/bin/bash" : null;
        }

        foreach (var root in _environment.WindowsGitRoots)
        {
            var candidate = Path.Combine(root, "Git", "bin", "bash.exe");
            if (_fileSystem.FileExists(candidate))
                return candidate;
        }

        foreach (var directory in SplitPath(_environment.ExecutablePath))
        {
            if (directory.StartsWith(_environment.SystemDirectory, StringComparison.OrdinalIgnoreCase))
                continue;
            var candidate = Path.Combine(directory, "bash.exe");
            if (_fileSystem.FileExists(candidate))
                return candidate;
        }
        return null;
    }

    private static IEnumerable<string> SplitPath(string? path)
        => (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
}

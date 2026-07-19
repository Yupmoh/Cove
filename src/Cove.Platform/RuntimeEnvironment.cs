namespace Cove.Platform;

public interface IRuntimeEnvironment
{
    bool IsWindows { get; }
    string? ExecutablePath { get; }
    string HomeDirectory { get; }
    string SystemDirectory { get; }
    IReadOnlyList<string> WindowsGitRoots { get; }
}

public sealed class SystemRuntimeEnvironment : IRuntimeEnvironment
{
    public static SystemRuntimeEnvironment Instance { get; } = new();

    public bool IsWindows => OperatingSystem.IsWindows();
    public string? ExecutablePath => Environment.GetEnvironmentVariable("PATH");
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string SystemDirectory => Environment.GetFolderPath(Environment.SpecialFolder.System);
    public IReadOnlyList<string> WindowsGitRoots =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
    ];
}

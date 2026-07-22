namespace Cove.Platform;

public interface IRuntimeEnvironment
{
    bool IsWindows { get; }
    bool IsMacOS { get; }
    bool IsLinux { get; }
    string? ExecutablePath { get; }
    string? UserExecutablePath { get; }
    string? MachineExecutablePath { get; }
    string? PathExtensions { get; }
    string HomeDirectory { get; }
    string SystemDirectory { get; }
    IReadOnlyList<string> WindowsGitRoots { get; }
    string? GetEnvironmentVariable(string name);
}

public sealed class SystemRuntimeEnvironment : IRuntimeEnvironment
{
    public static SystemRuntimeEnvironment Instance { get; } = new();

    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsMacOS => OperatingSystem.IsMacOS();
    public bool IsLinux => OperatingSystem.IsLinux();
    public string? ExecutablePath => Environment.GetEnvironmentVariable("PATH");
    public string? UserExecutablePath => IsWindows
        ? Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)
        : null;
    public string? MachineExecutablePath => IsWindows
        ? Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
        : null;
    public string? PathExtensions => Environment.GetEnvironmentVariable("PATHEXT");
    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string SystemDirectory => Environment.GetFolderPath(Environment.SpecialFolder.System);
    public IReadOnlyList<string> WindowsGitRoots =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
    ];

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);
}

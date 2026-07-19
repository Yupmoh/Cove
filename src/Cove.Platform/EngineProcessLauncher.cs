using System.Diagnostics;

namespace Cove.Platform;

public interface IEngineProcessLauncher
{
    void Launch(string channel);
}

public sealed class SystemEngineProcessLauncher : IEngineProcessLauncher
{
    private readonly IPlatformFileSystem _fileSystem;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Action<ProcessStartInfo> _startProcess;
    private readonly bool _isWindows;
    private readonly string _appDirectory;

    public static SystemEngineProcessLauncher Instance { get; } = new(
        SystemPlatformFileSystem.Instance,
        Environment.GetEnvironmentVariable,
        static startInfo =>
        {
            using var process = Process.Start(startInfo);
        },
        OperatingSystem.IsWindows(),
        AppContext.BaseDirectory);

    public SystemEngineProcessLauncher(
        IPlatformFileSystem fileSystem,
        Func<string, string?> getEnvironmentVariable,
        Action<ProcessStartInfo> startProcess,
        bool isWindows,
        string appDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(startProcess);
        ArgumentNullException.ThrowIfNull(appDirectory);
        _fileSystem = fileSystem;
        _getEnvironmentVariable = getEnvironmentVariable;
        _startProcess = startProcess;
        _isWindows = isWindows;
        _appDirectory = appDirectory;
    }

    public void Launch(string channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        var executable = ResolveExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        startInfo.ArgumentList.Add("daemon");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--channel");
        startInfo.ArgumentList.Add(channel);
        _startProcess(startInfo);
    }

    private string ResolveExecutable()
    {
        var configured = _getEnvironmentVariable("COVE_ENGINE");
        if (!string.IsNullOrEmpty(configured) && _fileSystem.FileExists(configured))
            return configured;

        var suffix = _isWindows ? ".exe" : "";
        var renamed = Path.Combine(_appDirectory, "cove-engine" + suffix);
        if (_fileSystem.FileExists(renamed))
            return renamed;

        var bundled = Path.Combine(_appDirectory, "cove" + suffix);
        if (_fileSystem.FileExists(bundled))
            return bundled;

        throw new FileNotFoundException(
            $"Engine executable was not found. Set COVE_ENGINE or install '{renamed}' or '{bundled}'.");
    }
}

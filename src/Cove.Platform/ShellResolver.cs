namespace Cove.Platform;

public interface IShellResolver
{
    string ResolveDefaultShell();
}

public sealed class SystemShellResolver : IShellResolver
{
    private readonly bool _isWindows;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public static SystemShellResolver Instance { get; } = new(
        OperatingSystem.IsWindows(),
        Environment.GetEnvironmentVariable);

    public SystemShellResolver(
        bool isWindows,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        _isWindows = isWindows;
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    public string ResolveDefaultShell()
    {
        if (_isWindows)
            return "powershell.exe";

        var configured = _getEnvironmentVariable("SHELL");
        return string.IsNullOrEmpty(configured) ? "/bin/zsh" : configured;
    }
}

public sealed record ShellInvocation(string Command, string[] Args)
{
    public static ShellInvocation Create(string shell, string commandLine)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(commandLine);
        var shellName = Path.GetFileNameWithoutExtension(shell);
        if (shellName.Equals("powershell", StringComparison.OrdinalIgnoreCase)
            || shellName.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            return new ShellInvocation(shell, ["-NoLogo", "-Command", commandLine]);
        }

        if (shellName.Equals("cmd", StringComparison.OrdinalIgnoreCase))
            return new ShellInvocation(shell, ["/c", commandLine]);

        return new ShellInvocation(shell, ["-ilc", commandLine]);
    }
}

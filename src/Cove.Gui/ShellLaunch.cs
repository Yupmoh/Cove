namespace Cove.Gui;

public static class ShellLaunch
{
    public static (string Command, string[] Args) Invocation(string shell, string commandLine)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(shell).ToLowerInvariant();
        if (name is "powershell" or "pwsh")
            return (shell, new[] { "-NoLogo", "-Command", commandLine });
        if (name is "cmd")
            return (shell, new[] { "/c", commandLine });
        return (shell, new[] { "-ilc", commandLine });
    }
}

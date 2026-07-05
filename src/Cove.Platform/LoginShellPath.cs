using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Cove.Platform;

public static class LoginShellPath
{
    public static string Probe(ILogger? logger = null)
    {
        var processPath = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (System.OperatingSystem.IsWindows())
            return processPath;

        try
        {
            var shell = System.Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrEmpty(shell) || !System.IO.File.Exists(shell))
                shell = System.IO.File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";

            var psi = new ProcessStartInfo(shell)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-lc");
            psi.ArgumentList.Add("printf %s \"$PATH\"");

            using var proc = Process.Start(psi);
            if (proc is null)
                return processPath;

            var outText = proc!.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(true); } catch { }
                return processPath;
            }

            outText = outText.Trim();
            return string.IsNullOrEmpty(outText) ? processPath : outText;
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Login shell PATH probe failed; falling back to process PATH. {Error}", ex);
            return processPath;
        }
    }
}

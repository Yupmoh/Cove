using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Platform;

public static class LoginShellPath
{
    public static string Probe(ILogger? logger = null)
    {
        ILogger log = logger ?? NullLogger.Instance;
        var processPath = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (System.OperatingSystem.IsWindows())
            return processPath;

        try
        {
            var shell = System.Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrEmpty(shell) || !System.IO.File.Exists(shell))
                shell = System.IO.File.Exists("/bin/zsh") ? "/bin/zsh" : "/bin/bash";
            log.LoginShellProbeBegin(shell);

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
            {
                log.LoginShellProbeFailed("Process.Start returned null");
                return processPath;
            }

            var outText = proc!.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(3000))
            {
                log.LoginShellProbeTimeout(shell);
                try { proc.Kill(true); }
                catch (Exception killEx) { log.LoginShellKillFailed(killEx.Message); }
                return processPath;
            }

            outText = outText.Trim();
            if (string.IsNullOrEmpty(outText))
            {
                log.LoginShellProbeEmpty(shell);
                return processPath;
            }
            log.LoginShellResolved(outText.Length);
            return outText;
        }
        catch (Exception ex)
        {
            log.LoginShellProbeFailed(ex.Message);
            return processPath;
        }
    }
}

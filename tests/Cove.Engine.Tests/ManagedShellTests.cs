using System.IO;
using Cove.Engine.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ManagedShellTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-shell-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void GenerateEnvSh_CreatesFileWithExports()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var content = File.ReadAllText(svc.EnvShPath);
            Assert.Contains("export COVE_DATA_DIR=", content);
            Assert.Contains("export COVE_CLI_PATH=", content);
            Assert.Contains("export __COVE_MANAGED_KEYS=", content);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void GenerateEnvSh_ContainsIdempotentPathAddition()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var content = File.ReadAllText(svc.EnvShPath);
            Assert.Contains("case \":$PATH:\"", content);
            Assert.Contains("export PATH=", content);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void GenerateEnvSh_CreatesFishCompatibleFile()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            Assert.True(File.Exists(svc.EnvFishPath));
            var content = File.ReadAllText(svc.EnvFishPath);
            Assert.Contains("set -x COVE_DATA_DIR", content);
            Assert.Contains("set -x COVE_CLI_PATH", content);
            Assert.DoesNotContain("export ", content);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ReinstallBinCove_CopiesCliWhenMissing()
    {
        var dir = NewDir();
        var fakeCli = Path.Combine(dir, "fake-cove");
        Directory.CreateDirectory(dir);
        File.WriteAllText(fakeCli, "#!/bin/sh\necho fake");
        try
        {
            var svc = new ManagedShellService(dir, fakeCli, NullLogger.Instance);
            svc.ReinstallBinCove();
            Assert.True(File.Exists(svc.BinCovePath));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ReinstallBinCove_SkipsWhenVersionMatches()
    {
        var dir = NewDir();
        var fakeCli = Path.Combine(dir, "fake-cove");
        Directory.CreateDirectory(dir);
        File.WriteAllText(fakeCli, "first");
        try
        {
            var svc = new ManagedShellService(dir, fakeCli, NullLogger.Instance);
            svc.ReinstallBinCove();
            var firstContent = File.ReadAllText(svc.BinCovePath);
            File.WriteAllText(fakeCli, "second");
            svc.ReinstallBinCove();
            var secondContent = File.ReadAllText(svc.BinCovePath);
            Assert.Equal(firstContent, secondContent);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ReinstallBinCove_ReinstallsOnVersionMismatch()
    {
        var dir = NewDir();
        var fakeCli = Path.Combine(dir, "fake-cove");
        Directory.CreateDirectory(dir);
        File.WriteAllText(fakeCli, "first");
        Directory.CreateDirectory(Path.Combine(dir, "bin"));
        File.WriteAllText(Path.Combine(dir, "bin", ".cove-version"), "0.0.0-old");
        try
        {
            var svc = new ManagedShellService(dir, fakeCli, NullLogger.Instance);
            svc.ReinstallBinCove();
            Assert.Contains("first", File.ReadAllText(svc.BinCovePath));
            Assert.Equal(Cove.Platform.CoveBuild.InformationalVersion, File.ReadAllText(Path.Combine(dir, "bin", ".cove-version")).Trim());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ReinstallBinCove_LogsWhenCliMissing()
    {
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/nonexistent/cove", NullLogger.Instance);
            svc.ReinstallBinCove();
            Assert.False(File.Exists(svc.BinCovePath));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void EnvSh_SourcesInBash()
    {
        if (System.OperatingSystem.IsWindows()) return;
        if (!ShellExists("bash")) return;
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var (exit, output) = RunShell("bash", svc.EnvShPath, "echo $COVE_DATA_DIR");
            Assert.Equal(0, exit);
            Assert.Contains(dir, output);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void EnvSh_SourcesInZsh()
    {
        if (!ShellExists("zsh")) return;
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var (exit, output) = RunShell("zsh", svc.EnvShPath, "echo $COVE_DATA_DIR");
            Assert.Equal(0, exit);
            Assert.Contains(dir, output);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void EnvFish_SourcesInFish()
    {
        if (!ShellExists("fish")) return;
        var dir = NewDir();
        try
        {
            var svc = new ManagedShellService(dir, "/fake/cove", NullLogger.Instance);
            svc.GenerateEnvSh();
            var (exit, output) = RunShell("fish", svc.EnvFishPath, "echo $COVE_DATA_DIR");
            Assert.Equal(0, exit);
            Assert.Contains(dir, output);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static bool ShellExists(string shell)
    {
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("which", shell) { RedirectStandardOutput = true, RedirectStandardError = true })!;
            p.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static (int exit, string output) RunShell(string shell, string envFile, string echoCmd)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(shell) { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-c");
        if (shell == "fish")
            psi.ArgumentList.Add($"source \"{envFile}\"; {echoCmd}");
        else
            psi.ArgumentList.Add($". \"{envFile}\"; {echoCmd}");
        var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit(5000);
        return (p.ExitCode, p.StandardOutput.ReadToEnd().Trim());
    }
}
